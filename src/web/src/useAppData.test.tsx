import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import type { GlobalSettings, Job, SyncSentinelConfig } from './types'

// Fake SignalR connection: records the hub handlers useHub registers so a test
// can fire 'runStarted' / 'runFinished' and exercise the real useHub → useAppData
// path. The websocket library is the I/O seam; everything above it runs for real.
const hub = vi.hoisted(() => {
  const handlers: Record<string, (...args: unknown[]) => void> = {}
  return {
    handlers,
    reset: () => Object.keys(handlers).forEach((k) => delete handlers[k]),
    connection: {
      on: (event: string, h: (...args: unknown[]) => void) => { handlers[event] = h },
      start: () => Promise.resolve(),
      stop: () => Promise.resolve(),
    },
  }
})

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    build() { return hub.connection }
  },
}))

vi.mock('./api', () => ({
  api: {
    getConfig: vi.fn(),
    getJobStatuses: vi.fn(),
    getRecentRuns: vi.fn(),
    getStats: vi.fn(),
    runJob: vi.fn(),
    deleteJob: vi.fn(),
  },
}))
import { api } from './api'
import { useAppData } from './useAppData'

const SETTINGS: GlobalSettings = {
  defaultFlags: '/MIR', defaultIntervalMinutes: 15, maxConcurrent: 1,
  retention: { runsPerJob: 100, days: 30 }, autostart: true,
}
const job = (id: string): Job => ({
  id, name: id, source: 's', destination: 'd',
  folderSetIds: [], fileSetIds: [], flagsOverride: null, intervalMinutes: 15, enabled: true,
})
const config = (...jobs: Job[]): SyncSentinelConfig =>
  ({ jobs, folderSets: [], fileSets: [], settings: SETTINGS })

beforeEach(() => {
  vi.clearAllMocks()
  hub.reset()
  vi.mocked(api.getConfig).mockResolvedValue(config(job('j1')))
  vi.mocked(api.getJobStatuses).mockResolvedValue([
    { jobId: 'j1', lastStatus: 'Success', nextDueUtc: null, state: 'Idle' },
  ])
  vi.mocked(api.getRecentRuns).mockResolvedValue([])
  vi.mocked(api.getStats).mockResolvedValue({ runs: 2, filesCopied: 5, failures: 0 })
})

describe('useAppData', () => {
  it('loads config, statuses (keyed by job id), and dashboard on mount', async () => {
    const { result } = renderHook(() => useAppData())

    await waitFor(() => expect(result.current.config).not.toBeNull())
    expect(result.current.config!.jobs).toHaveLength(1)
    expect(result.current.statuses['j1'].lastStatus).toBe('Success')
    expect(result.current.stats).toEqual({ runs: 2, filesCopied: 5, failures: 0 })
  })

  it('marks a job running optimistically when runJob is called', async () => {
    vi.mocked(api.runJob).mockResolvedValue({ ok: true } as Response)
    const { result } = renderHook(() => useAppData())
    await waitFor(() => expect(result.current.config).not.toBeNull())

    await act(async () => { await result.current.runJob('j1') })

    expect(result.current.isJobRunning('j1')).toBe(true)
    expect(api.runJob).toHaveBeenCalledWith('j1')
  })

  it('clears optimism when the run request is rejected', async () => {
    vi.mocked(api.runJob).mockResolvedValue({ ok: false } as Response)
    const { result } = renderHook(() => useAppData())
    await waitFor(() => expect(result.current.config).not.toBeNull())

    await act(async () => { await result.current.runJob('j1') })

    expect(result.current.isJobRunning('j1')).toBe(false)
  })

  it('clears optimism and refetches the feed when a run finishes', async () => {
    vi.mocked(api.runJob).mockResolvedValue({ ok: true } as Response)
    const { result } = renderHook(() => useAppData())
    await waitFor(() => expect(result.current.config).not.toBeNull())

    await act(async () => { await result.current.runJob('j1') })
    expect(result.current.isJobRunning('j1')).toBe(true)
    const statusCallsBefore = vi.mocked(api.getJobStatuses).mock.calls.length

    // The status hub broadcasts the run finishing.
    act(() => hub.handlers.runFinished?.('Success', 0))

    await waitFor(() => expect(result.current.isJobRunning('j1')).toBe(false))
    expect(vi.mocked(api.getJobStatuses).mock.calls.length).toBeGreaterThan(statusCallsBefore)
  })

  it('reports a hub-broadcast run as running even without local optimism', async () => {
    const { result } = renderHook(() => useAppData())
    await waitFor(() => expect(result.current.config).not.toBeNull())

    // A run this client did not start (another client, or the scheduler).
    act(() => hub.handlers.runStarted?.('j1', 'j1'))

    expect(result.current.isJobRunning('j1')).toBe(true)
    expect(result.current.run.jobId).toBe('j1')
  })

  it('deleteJob removes the job then refreshes the config', async () => {
    vi.mocked(api.deleteJob).mockResolvedValue({ ok: true } as Response)
    vi.mocked(api.getConfig).mockResolvedValueOnce(config(job('j1'))).mockResolvedValue(config())
    const { result } = renderHook(() => useAppData())
    await waitFor(() => expect(result.current.config!.jobs).toHaveLength(1))

    await act(async () => { await result.current.deleteJob('j1') })

    expect(api.deleteJob).toHaveBeenCalledWith('j1')
    await waitFor(() => expect(result.current.config!.jobs).toHaveLength(0))
  })
})
