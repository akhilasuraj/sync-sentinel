import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { useHub, type RunInfo } from './useHub'
import type { RunRecord, RunStats, SyncSentinelConfig } from './types'
import type { JobStatus } from './lib/jobStatus'

export interface AppData {
  config: SyncSentinelConfig | null
  statuses: Record<string, JobStatus>
  recent: RunRecord[]
  stats: RunStats | null
  connected: boolean
  run: RunInfo
  isJobRunning: (jobId: string) => boolean
  runJob: (jobId: string) => Promise<void>
  deleteJob: (jobId: string) => Promise<void>
  refresh: () => void
}

/**
 * Owns the app's live data: the config document, the per-job status feed, and
 * the dashboard aggregates. The single home for fetching + refetching, so views
 * stay presentational. (Live-run wiring and optimistic run tracking are added by
 * later cycles.)
 */
export function useAppData(): AppData {
  const [config, setConfig] = useState<SyncSentinelConfig | null>(null)
  const [statuses, setStatuses] = useState<Record<string, JobStatus>>({})
  const [recent, setRecent] = useState<RunRecord[]>([])
  const [stats, setStats] = useState<RunStats | null>(null)
  const [runningId, setRunningId] = useState<string | null>(null)

  // Live run state from the status hub; clear the optimistic flag on finish.
  const { connected, run } = useHub(() => setRunningId(null))

  const reload = useCallback(() => api.getConfig().then(setConfig), [])
  const reloadStatuses = useCallback(
    () => api.getJobStatuses().then((list) => setStatuses(Object.fromEntries(list.map((s) => [s.jobId, s])))),
    [],
  )
  const reloadDashboard = useCallback(() => {
    api.getRecentRuns(10).then(setRecent).catch(() => {})
    api.getStats().then(setStats).catch(() => {})
  }, [])

  const runJob = useCallback(async (jobId: string) => {
    setRunningId(jobId) // optimistic: show running before the feed/hub catches up
    const res = await api.runJob(jobId)
    if (!res.ok) setRunningId(null) // 409 already running / 422 precondition failed
  }, [])

  const isJobRunning = useCallback(
    (jobId: string) => runningId === jobId || (run.state === 'running' && run.jobId === jobId),
    [runningId, run.state, run.jobId],
  )

  const deleteJob = useCallback(async (jobId: string) => {
    await api.deleteJob(jobId)
    await reload()
  }, [reload])

  useEffect(() => void reload(), [reload])
  // Refresh the feed + dashboard on load and on every run-state change.
  useEffect(() => {
    reloadStatuses()
    reloadDashboard()
  }, [reloadStatuses, reloadDashboard, run.state])

  return { config, statuses, recent, stats, connected, run, isJobRunning, runJob, deleteJob, refresh: reload }
}
