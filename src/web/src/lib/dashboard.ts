// Pure derivations for the dashboard, computed from the config jobs + the
// /api/jobs/status feed so they're unit-testable without rendering.

import type { Job } from '../types'
import type { JobStatus } from './jobStatus'

export interface FleetSummary {
  total: number
  watching: number // enabled (scheduled)
  paused: number
  attention: number // last run Error or Skipped
}

export function fleetSummary(jobs: Job[], statusById: Record<string, JobStatus>): FleetSummary {
  const watching = jobs.filter((j) => j.enabled).length
  const attention = jobs.filter((j) => {
    const s = statusById[j.id]?.lastStatus
    return s === 'Error' || s === 'Skipped'
  }).length
  return { total: jobs.length, watching, paused: jobs.length - watching, attention }
}

export interface NextUp {
  job: Job
  status: JobStatus
}

/** The job to feature on the watch hero: the running one, else the soonest due. */
export function nextUp(jobs: Job[], statusById: Record<string, JobStatus>, _now: number): NextUp | null {
  const paired = jobs
    .map((job) => ({ job, status: statusById[job.id] }))
    .filter((p): p is NextUp => p.status != null)

  const running = paired.find((p) => p.status.state === 'Running')
  if (running) return running

  const scheduled = paired
    .filter((p) => p.job.enabled && p.status.nextDueUtc)
    .sort((a, b) => Date.parse(a.status.nextDueUtc!) - Date.parse(b.status.nextDueUtc!))
  return scheduled[0] ?? null
}
