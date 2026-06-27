import { useEffect, useRef, useState } from 'react'
import { api } from '../api'
import type { Job, RunRecord } from '../types'
import type { RunInfo } from '../lib/runState'
import { cardState, type JobStatus } from '../lib/jobStatus'
import { formatDuration, runCounts, statusDotClass } from '../lib/runFormat'
import ConfirmDialog from './ConfirmDialog'

interface Props {
  job: Job
  status?: JobStatus
  now: number
  run: RunInfo
  isRunning: boolean
  onBack: () => void
  onRun: () => void
  onEdit: () => void
  onDelete: () => void
}

const IDLE: JobStatus = { jobId: '', lastStatus: null, nextDueUtc: null, state: 'Idle' }

export default function JobDetails({ job, status, now, run, isRunning, onBack, onRun, onEdit, onDelete }: Props) {
  const [runs, setRuns] = useState<RunRecord[] | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)
  const [logs, setLogs] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState(false)
  const liveRef = useRef<HTMLPreElement>(null)

  // Reload this job's history on mount and whenever a run settles.
  useEffect(() => {
    api.getRuns(job.id).then(setRuns).catch(() => setRuns([]))
  }, [job.id, run.state])

  // Keep the live log pinned to the newest line.
  useEffect(() => {
    const el = liveRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [run.lines])

  const effective: JobStatus = isRunning ? { ...(status ?? IDLE), state: 'Running' } : status ?? IDLE
  const { dot, label } = cardState(effective, job.enabled, now)

  // The hub's current run is THIS job's: show it while running (even before the
  // first output line) or once it's finished with its lines still captured.
  const live = run.jobId === job.id && (run.state === 'running' || run.lines.length > 0)

  function toggleLog(record: RunRecord) {
    if (openId === record.id) {
      setOpenId(null)
      return
    }
    setOpenId(record.id)
    if (logs[record.id] === undefined) {
      api
        .getRunLog(record.id)
        .then((text) => setLogs((l) => ({ ...l, [record.id]: text })))
        .catch(() => setLogs((l) => ({ ...l, [record.id]: '(log unavailable)' })))
    }
  }

  return (
    <div className="mx-auto max-w-4xl px-8 py-8">
      <button onClick={onBack} className="text-sm text-slate-400 transition hover:text-slate-200">‹ Jobs</button>

      <header className="mt-4 flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2.5">
            <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${dot}`} />
            <h1 className="truncate text-2xl font-semibold text-slate-100">{job.name}</h1>
            <span className="font-mono text-xs text-slate-500">{label}</span>
          </div>
          <p className="mt-2 truncate font-mono text-xs text-slate-400">
            {job.source} <span className="text-sentinel">→</span> {job.destination}
          </p>
        </div>
        <div className="flex shrink-0 gap-2">
          <button className="btn" disabled={isRunning} onClick={onRun}>{isRunning ? 'Running…' : 'Run now'}</button>
          <button className="btn-ghost" onClick={onEdit}>Edit</button>
          <button className="btn-ghost text-red-400" onClick={() => setConfirming(true)}>Delete</button>
        </div>
      </header>

      {/* ── Live ──────────────────────────────────────────────────────── */}
      <section className="mt-8">
        <p className="eyebrow">Live</p>
        {live ? (
          <pre
            ref={liveRef}
            className="mt-3 max-h-64 overflow-auto rounded-xl border border-edge bg-panel-2 p-4 font-mono text-[12px] leading-relaxed break-words whitespace-pre-wrap text-slate-300"
          >
            {run.lines.join('\n')}
          </pre>
        ) : (
          <p className="mt-3 rounded-xl border border-dashed border-edge bg-panel-2 px-4 py-6 text-center text-sm text-slate-600">
            Not running. Open a history entry below to view its log.
          </p>
        )}
      </section>

      {/* ── History (accordion: one log open at a time) ──────────────────── */}
      <section className="mt-8">
        <p className="eyebrow">History</p>
        {runs === null && <p className="mt-3 text-sm text-slate-500">Loading…</p>}
        {runs?.length === 0 && <p className="mt-3 text-sm text-slate-500">No runs yet.</p>}

        <div className="mt-3 space-y-2">
          {runs?.map((record) => {
            const open = openId === record.id
            return (
              <div key={record.id} className="overflow-hidden rounded-xl border border-edge bg-panel">
                <button
                  onClick={() => toggleLog(record)}
                  aria-expanded={open}
                  className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-white/5"
                >
                  <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${statusDotClass(record.status)}`} />
                  <span className="w-44 shrink-0 font-mono text-xs text-slate-300">{new Date(record.finishedUtc).toLocaleString()}</span>
                  <span className="flex-1 truncate font-mono text-xs text-slate-500">{runCounts(record)}</span>
                  <span className="shrink-0 font-mono text-xs text-slate-500">{formatDuration(record.durationSeconds)}</span>
                  <span className={`shrink-0 text-slate-600 transition ${open ? 'rotate-90 text-slate-300' : ''}`} aria-hidden>›</span>
                </button>
                {open && (
                  <pre className="max-h-72 overflow-auto border-t border-edge bg-panel-2 p-4 font-mono text-[12px] leading-relaxed break-words whitespace-pre-wrap text-slate-300">
                    {logs[record.id] ?? 'Loading…'}
                  </pre>
                )}
              </div>
            )
          })}
        </div>
      </section>

      {confirming && (
        <ConfirmDialog
          title="Delete job?"
          message={`Delete “${job.name}”? This removes the job and its run history. This can't be undone.`}
          onConfirm={() => { setConfirming(false); onDelete() }}
          onCancel={() => setConfirming(false)}
        />
      )}
    </div>
  )
}
