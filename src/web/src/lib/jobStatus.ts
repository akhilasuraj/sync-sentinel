// Pure logic for the job card's status dot + next-run countdown, fed by the
// /api/jobs/status feed. Kept here (not in the component) so it's unit-testable
// without rendering. See the agreed card state machine in the UI plan.

import { statusDotClass } from './runFormat'

export interface JobStatus {
  jobId: string
  lastStatus: string | null // 'Success' | 'Warning' | 'Error' | 'Skipped' | null (never run)
  nextDueUtc: string | null // null when the job is paused
  state: 'Running' | 'Queued' | 'Idle'
}

/** Human countdown to the next scheduled run, e.g. "next in 4m" / "Due now". */
export function formatCountdown(nextDueUtc: string, now: number): string {
  const diff = Date.parse(nextDueUtc) - now
  if (diff <= 0) return 'Due now'
  const sec = Math.floor(diff / 1000)
  if (sec < 60) return `next in ${sec}s`
  const min = Math.floor(sec / 60)
  if (min < 60) return `next in ${min}m`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `next in ${hr}h ${min % 60}m`
  return `next in ${Math.floor(hr / 24)}d ${hr % 24}h`
}

/** Clock-style readout for the dashboard watch hero: "04:12" / "1:04:05" / "Due". */
export function clockCountdown(nextDueUtc: string, now: number): string {
  const diff = Date.parse(nextDueUtc) - now
  if (diff <= 0) return 'Due'
  const total = Math.floor(diff / 1000)
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  const pad = (n: number) => String(n).padStart(2, '0')
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`
}

/** The job card's dot colour + label, resolving the agreed state machine. */
export function cardState(status: JobStatus, enabled: boolean, now: number): { dot: string; label: string } {
  if (status.state === 'Running') return { dot: 'bg-amber-400 animate-pulse', label: 'Running…' }
  if (status.state === 'Queued') return { dot: 'bg-slate-400 animate-pulse', label: 'Queued' }
  if (!enabled) return { dot: 'bg-slate-600', label: 'Paused' }
  if (status.lastStatus === null) return { dot: 'bg-slate-500', label: 'Not run yet' }
  return {
    dot: statusDotClass(status.lastStatus),
    label: status.nextDueUtc ? formatCountdown(status.nextDueUtc, now) : 'Due now',
  }
}
