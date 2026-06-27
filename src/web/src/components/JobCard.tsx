import type { Job } from '../types'
import { cardState, type JobStatus } from '../lib/jobStatus'

interface Props {
  job: Job
  status?: JobStatus
  now: number
  isRunning: boolean
  onRun: () => void
  onEdit: () => void
  onDelete: () => void
  onHistory: () => void
}

const IDLE: JobStatus = { jobId: '', lastStatus: null, nextDueUtc: null, state: 'Idle' }

/** A single job row: a status dot + next-run label, paths, and actions. */
export default function JobCard({ job, status, now, isRunning, onRun, onEdit, onDelete, onHistory }: Props) {
  // The parent flips isRunning the instant Run is clicked, before the status
  // feed catches up — fold it in so the dot/label react immediately.
  const effective: JobStatus = isRunning ? { ...(status ?? IDLE), state: 'Running' } : status ?? IDLE
  const { dot, label } = cardState(effective, job.enabled, now)

  return (
    <div className="flex items-center justify-between rounded-2xl border border-edge bg-panel p-4">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${dot}`} />
          <span className="truncate font-semibold">{job.name}</span>
          <span className="rounded bg-slate-700/50 px-1.5 text-xs text-slate-400">{label}</span>
        </div>
        <div className="mt-1 truncate font-mono text-xs text-slate-400">{job.source} → {job.destination}</div>
      </div>
      <div className="flex shrink-0 gap-2">
        <button className="btn-ghost" disabled={isRunning} onClick={onRun}>{isRunning ? 'Running…' : 'Run now'}</button>
        <button className="btn-ghost" onClick={onHistory}>History</button>
        <button className="btn-ghost" onClick={onEdit}>Edit</button>
        <button className="btn-ghost text-red-400" onClick={onDelete}>Delete</button>
      </div>
    </div>
  )
}
