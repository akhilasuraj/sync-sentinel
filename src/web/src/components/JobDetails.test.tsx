import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import JobDetails from './JobDetails'
import { blankJob } from '../types'
import { IDLE, type RunInfo } from '../lib/runState'

vi.mock('../api', () => ({
  api: {
    getRuns: vi.fn().mockResolvedValue([
      {
        id: 'r1', jobId: 'j1', jobName: 'PEMS', status: 'Success',
        startedUtc: '2026-06-26T12:00:00Z', finishedUtc: '2026-06-26T12:00:05Z',
        filesCopied: 4, filesSkipped: 0, filesFailed: 0, filesExtra: 1,
        exitCode: 1, logPath: 'x.log', durationSeconds: 5,
      },
      {
        id: 'r2', jobId: 'j1', jobName: 'PEMS', status: 'Error',
        startedUtc: '2026-06-26T11:00:00Z', finishedUtc: '2026-06-26T11:00:02Z',
        filesCopied: 0, filesSkipped: 0, filesFailed: 2, filesExtra: 0,
        exitCode: 16, logPath: 'y.log', durationSeconds: 2,
      },
    ]),
    getRunLog: vi.fn((id: string) => Promise.resolve(id === 'r1' ? 'LOG ONE' : 'LOG TWO')),
  },
}))
import { api } from '../api'

const job = { ...blankJob(), id: 'j1', name: 'PEMS', source: 'C:\\dev\\PEMS', destination: 'D:\\bak\\PEMS' }
const noop = () => {}
const render_ = () =>
  render(<JobDetails job={job} now={Date.parse('2026-06-27T12:00:00Z')} run={IDLE} isRunning={false} onBack={noop} onRun={noop} onEdit={noop} onDelete={noop} />)

describe('JobDetails history', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows the Live panel as soon as the job is running, before any output', () => {
    const running: RunInfo = { state: 'running', jobId: 'j1', jobName: 'PEMS', exitCode: null, lines: [] }
    render(<JobDetails job={job} now={Date.parse('2026-06-27T12:00:00Z')} run={running} isRunning onBack={noop} onRun={noop} onEdit={noop} onDelete={noop} />)
    expect(screen.queryByText(/Not running/)).not.toBeInTheDocument()
  })

  it('lists the job runs with their counts', async () => {
    render_()
    expect(await screen.findByText(/4 copied · 1 extra · 0 failed/)).toBeInTheDocument()
    expect(api.getRuns).toHaveBeenCalledWith('j1')
  })

  it('toggles a run log inline when its entry is clicked', async () => {
    const user = userEvent.setup()
    render_()

    await user.click(await screen.findByText(/4 copied/))
    expect(api.getRunLog).toHaveBeenCalledWith('r1')
    expect(await screen.findByText('LOG ONE')).toBeInTheDocument()

    await user.click(screen.getByText(/4 copied/)) // click again collapses it
    expect(screen.queryByText('LOG ONE')).not.toBeInTheDocument()
  })

  it('keeps only one log open at a time', async () => {
    const user = userEvent.setup()
    render_()

    await user.click(await screen.findByText(/4 copied/))
    expect(await screen.findByText('LOG ONE')).toBeInTheDocument()

    await user.click(screen.getByText(/2 failed/))
    expect(await screen.findByText('LOG TWO')).toBeInTheDocument()
    expect(screen.queryByText('LOG ONE')).not.toBeInTheDocument()
  })
})
