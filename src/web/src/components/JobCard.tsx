import type { Job } from '../types'

interface Props {
  job: Job
  isRunning: boolean
  onRun: () => void
  onEdit: () => void
  onDelete: () => void
}

/** A single job row on the dashboard: status dot, name, paths, and actions. */
export default function JobCard({ job, isRunning, onRun, onEdit, onDelete }: Props) {
  const dot = isRunning ? 'bg-amber-400 animate-pulse' : job.enabled ? 'bg-slate-500' : 'bg-slate-700'

  return (
    <div className="flex items-center justify-between rounded-2xl border border-edge bg-panel p-4">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${dot}`} />
          <span className="truncate font-semibold">{job.name}</span>
          {job.enabled ? (
            <span className="rounded bg-slate-700/50 px-1.5 text-xs text-slate-400">every {job.intervalMinutes}m</span>
          ) : (
            <span className="rounded bg-slate-700/50 px-1.5 text-xs text-slate-400">paused</span>
          )}
        </div>
        <div className="mt-1 truncate font-mono text-xs text-slate-400">{job.source} → {job.destination}</div>
      </div>
      <div className="flex shrink-0 gap-2">
        <button className="btn-ghost" disabled={isRunning} onClick={onRun}>{isRunning ? 'Running…' : 'Run now'}</button>
        <button className="btn-ghost" onClick={onEdit}>Edit</button>
        <button className="btn-ghost text-red-400" onClick={onDelete}>Delete</button>
      </div>
    </div>
  )
}
