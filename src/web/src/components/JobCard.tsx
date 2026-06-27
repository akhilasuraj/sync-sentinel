import type { Job } from '../types'
import { cardState, type JobStatus } from '../lib/jobStatus'

interface Props {
  job: Job
  status?: JobStatus
  now: number
  isRunning: boolean
  onOpen: () => void
  onRun: () => void
}

const IDLE: JobStatus = { jobId: '', lastStatus: null, nextDueUtc: null, state: 'Idle' }

/** A job row in the list: status dot + next-run label, paths, a quick Run, and a
 *  click anywhere to open the job's detail page. */
export default function JobCard({ job, status, now, isRunning, onOpen, onRun }: Props) {
  // The parent flips isRunning the instant Run is clicked, before the feed
  // catches up — fold it in so the dot/label react immediately.
  const effective: JobStatus = isRunning ? { ...(status ?? IDLE), state: 'Running' } : status ?? IDLE
  const { dot, label } = cardState(effective, job.enabled, now)

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => {
        // Only the card itself opens on Enter/Space — ignore keys bubbling up
        // from the nested Run button (else it would run AND open).
        if (e.target !== e.currentTarget) return
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onOpen()
        }
      }}
      className="group flex cursor-pointer items-center justify-between rounded-2xl border border-edge bg-panel p-4 transition hover:border-slate-600 focus:outline-none focus-visible:border-sentinel"
    >
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${dot}`} />
          <span className="truncate font-semibold">{job.name}</span>
          <span className="font-mono text-xs text-slate-500">{label}</span>
        </div>
        <div className="mt-1 truncate font-mono text-xs text-slate-400">{job.source} → {job.destination}</div>
      </div>
      <div className="flex shrink-0 items-center gap-3">
        <button className="btn-ghost" disabled={isRunning} onClick={(e) => { e.stopPropagation(); onRun() }}>
          {isRunning ? 'Running…' : 'Run now'}
        </button>
        <span className="text-slate-600 transition group-hover:text-slate-300" aria-hidden>›</span>
      </div>
    </div>
  )
}
