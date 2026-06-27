import { useEffect, useState } from 'react'
import { api } from '../api'
import { formatDuration, runCounts, statusDotClass } from '../lib/runFormat'
import type { RunRecord } from '../types'

interface Props {
  jobId: string
  jobName: string
  onClose: () => void
}

export default function JobHistory({ jobId, jobName, onClose }: Props) {
  const [runs, setRuns] = useState<RunRecord[] | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [log, setLog] = useState<string>('')

  useEffect(() => {
    api.getRuns(jobId).then(setRuns).catch(() => setRuns([]))
  }, [jobId])

  async function openLog(run: RunRecord) {
    setSelected(run.id)
    setLog('Loading…')
    try {
      setLog(await api.getRunLog(run.id))
    } catch {
      setLog('(log unavailable)')
    }
  }

  return (
    <div className="fixed inset-0 z-10 grid place-items-center bg-black/50 p-4" onClick={onClose}>
      <div
        className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-edge bg-panel p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mb-4 text-lg font-semibold">History · {jobName}</h2>

        {runs === null && <p className="text-sm text-slate-500">Loading…</p>}
        {runs?.length === 0 && <p className="text-sm text-slate-500">No runs yet.</p>}

        <div className="min-h-0 flex-1 space-y-2 overflow-auto">
          {runs?.map((run) => (
            <button
              key={run.id}
              onClick={() => openLog(run)}
              className={`flex w-full items-center gap-3 rounded-lg border px-3 py-2 text-left transition ${
                selected === run.id ? 'border-sentinel bg-panel-2' : 'border-edge bg-panel-2 hover:border-slate-600'
              }`}
            >
              <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${statusDotClass(run.status)}`} />
              <span className="w-40 shrink-0 text-sm text-slate-300">{new Date(run.finishedUtc).toLocaleString()}</span>
              <span className="flex-1 text-sm text-slate-400">{runCounts(run)}</span>
              <span className="shrink-0 text-xs tabular-nums text-slate-500">{formatDuration(run.durationSeconds)}</span>
            </button>
          ))}
        </div>

        {selected && (
          <pre className="mt-4 max-h-64 shrink-0 overflow-auto rounded-lg border border-edge bg-panel-2 p-3 font-mono text-[12px] leading-relaxed whitespace-pre-wrap">
            {log}
          </pre>
        )}

        <div className="mt-4 flex justify-end">
          <button className="btn-ghost" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  )
}
