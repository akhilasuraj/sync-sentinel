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

/**
 * Font-size class for the watch-ring countdown. The ring's clear area is ~104px,
 * so longer readouts shrink to stay inside the stroke: MM:SS at full size,
 * H:MM:SS smaller, multi-digit hours (e.g. "10:00:00") smaller still — no cap on
 * hours in clockCountdown, so size by length rather than assume a max width.
 */
export function ringLabelSizeClass(label: string): string {
  if (label.length <= 5) return 'text-[24px]'
  if (label.length <= 7) return 'text-[20px]'
  return 'text-[16px]'
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
