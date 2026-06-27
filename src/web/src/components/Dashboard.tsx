import type { Job, RunRecord, RunStats } from '../types'
import type { JobStatus } from '../lib/jobStatus'
import { clockCountdown } from '../lib/jobStatus'
import { fleetSummary, nextUp } from '../lib/dashboard'
import { formatDuration, runCounts, statusDotClass } from '../lib/runFormat'

interface Props {
  jobs: Job[]
  statusById: Record<string, JobStatus>
  now: number
  recent: RunRecord[]
  stats: RunStats | null
  onOpenJob: (jobId: string) => void
  onCreate: () => void
}

export default function Dashboard({ jobs, statusById, now, recent, stats, onOpenJob, onCreate }: Props) {
  const fleet = fleetSummary(jobs, statusById)
  const featured = nextUp(jobs, statusById, now)

  return (
    <div className="mx-auto max-w-5xl px-8 py-8">
      <p className="eyebrow">Dashboard</p>

      {/* ── Signature: the watch ───────────────────────────────────────── */}
      <section className="mt-3 overflow-hidden rounded-2xl border border-edge bg-panel">
        {jobs.length === 0 ? (
          <EmptyWatch onCreate={onCreate} />
        ) : featured ? (
          <Watch featured={featured} now={now} onOpen={() => onOpenJob(featured.job.id)} />
        ) : (
          <IdleWatch />
        )}
      </section>

      {/* ── Fleet health tiles ─────────────────────────────────────────── */}
      <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Tile value={fleet.total} label="Jobs" />
        <Tile value={fleet.watching} label="Watching" accent="sentinel" />
        <Tile value={fleet.paused} label="Paused" />
        <Tile value={fleet.attention} label="Need attention" accent={fleet.attention > 0 ? 'warn' : undefined} />
      </div>

      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-5">
        {/* ── 7-day aggregate ──────────────────────────────────────────── */}
        <section className="lg:col-span-2 rounded-2xl border border-edge bg-panel p-6">
          <p className="eyebrow">Last 7 days</p>
          {stats && stats.runs > 0 ? (
            <div className="mt-4 space-y-4">
              <Metric value={stats.filesCopied.toLocaleString()} label="files copied" />
              <div className="grid grid-cols-2 gap-4">
                <Metric value={`${Math.round(((stats.runs - stats.failures) / stats.runs) * 100)}%`} label="success rate" small />
                <Metric value={String(stats.runs)} label="runs" small />
              </div>
            </div>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No runs in the last 7 days.</p>
          )}
        </section>

        {/* ── Recent activity ──────────────────────────────────────────── */}
        <section className="lg:col-span-3 rounded-2xl border border-edge bg-panel p-6">
          <p className="eyebrow">Recent activity</p>
          {recent.length === 0 ? (
            <p className="mt-4 text-sm text-slate-500">Nothing has run yet.</p>
          ) : (
            <ul className="mt-3 divide-y divide-edge/60">
              {recent.map((run) => (
                <li key={run.id}>
                  <button
                    onClick={() => onOpenJob(run.jobId)}
                    className="flex w-full items-center gap-3 py-2.5 text-left transition hover:opacity-80"
                  >
                    <span className={`h-2 w-2 shrink-0 rounded-full ${statusDotClass(run.status)}`} />
                    <span className="w-32 shrink-0 truncate text-sm text-slate-200">{run.jobName}</span>
                    <span className="flex-1 truncate font-mono text-xs text-slate-500">{runCounts(run)}</span>
                    <span className="shrink-0 font-mono text-xs text-slate-500">
                      {new Date(run.finishedUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </span>
                    <span className="w-12 shrink-0 text-right font-mono text-xs text-slate-600">{formatDuration(run.durationSeconds)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}

// ── The watch instrument ─────────────────────────────────────────────────

function Watch({ featured, now, onOpen }: { featured: { job: Job; status: JobStatus }; now: number; onOpen: () => void }) {
  const running = featured.status.state === 'Running'
  const interval = featured.job.intervalMinutes * 60_000
  const due = featured.status.nextDueUtc ? Date.parse(featured.status.nextDueUtc) : now
  const progress = running ? 1 : interval > 0 ? (interval - (due - now)) / interval : 0

  return (
    <div className="flex flex-col items-center gap-8 p-7 sm:flex-row sm:items-center sm:gap-10">
      <Ring
        progress={progress}
        label={running ? 'scan' : clockCountdown(featured.status.nextDueUtc ?? '', now)}
        sub={running ? 'scanning' : 'to next sweep'}
        pulse={running}
      />
      <div className="min-w-0 flex-1 text-center sm:text-left">
        <p className="eyebrow">Next sweep</p>
        <h2 className="mt-1 truncate text-2xl font-semibold text-slate-100">{featured.job.name}</h2>
        <p className="mt-2 truncate font-mono text-xs text-slate-500">
          {featured.job.source} <span className="text-sentinel">→</span> {featured.job.destination}
        </p>
        <button onClick={onOpen} className="btn-ghost mt-4">Open job</button>
      </div>
    </div>
  )
}

function IdleWatch() {
  return (
    <div className="p-10 text-center">
      <p className="eyebrow">Next sweep</p>
      <p className="mt-2 text-slate-400">No jobs on watch. Enable a job to start the schedule.</p>
    </div>
  )
}

function EmptyWatch({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="p-10 text-center">
      <p className="eyebrow">Standing by</p>
      <h2 className="mt-2 text-xl font-semibold text-slate-100">Nothing to watch yet</h2>
      <p className="mx-auto mt-1 max-w-sm text-sm text-slate-500">
        Point SyncSentinel at a folder and it will mirror it to safety on a schedule.
      </p>
      <button onClick={onCreate} className="btn mt-5">Create your first job</button>
    </div>
  )
}

function Ring({ progress, label, sub, pulse }: { progress: number; label: string; sub: string; pulse?: boolean }) {
  const r = 54
  const c = 2 * Math.PI * r
  const clamped = Math.max(0, Math.min(1, progress))
  return (
    <div className="relative grid shrink-0 place-items-center" style={{ width: 148, height: 148 }}>
      <svg width={148} height={148} className="-rotate-90">
        <circle cx={74} cy={74} r={r} fill="none" stroke="var(--color-edge)" strokeWidth={5} />
        <circle
          cx={74}
          cy={74}
          r={r}
          fill="none"
          stroke="var(--color-sentinel)"
          strokeWidth={5}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={c * (1 - clamped)}
          className={pulse ? 'animate-pulse' : ''}
          style={{ transition: 'stroke-dashoffset 1s linear' }}
        />
      </svg>
      <div className="absolute text-center">
        <div className="font-mono text-[28px] font-semibold tabular-nums text-slate-100">{label}</div>
        <div className="eyebrow mt-1">{sub}</div>
      </div>
    </div>
  )
}

function Tile({ value, label, accent }: { value: number; label: string; accent?: 'sentinel' | 'warn' }) {
  const tone =
    accent === 'sentinel' ? 'text-sentinel' : accent === 'warn' ? 'text-amber-400' : 'text-slate-100'
  return (
    <div className="rounded-xl border border-edge bg-panel p-4">
      <div className={`font-mono text-3xl font-semibold tabular-nums ${tone}`}>{value}</div>
      <div className="mt-1 text-xs text-slate-500">{label}</div>
    </div>
  )
}

function Metric({ value, label, small }: { value: string; label: string; small?: boolean }) {
  return (
    <div>
      <div className={`font-mono font-semibold tabular-nums text-slate-100 ${small ? 'text-xl' : 'text-4xl'}`}>{value}</div>
      <div className="mt-1 text-xs text-slate-500">{label}</div>
    </div>
  )
}
