import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { useHub, type RunState } from './useHub'
import { blankJob, type Job, type SyncSentinelConfig } from './types'
import JobEditor from './components/JobEditor'
import SetsTab from './components/SetsTab'
import SettingsTab from './components/SettingsTab'

type Tab = 'jobs' | 'sets' | 'settings'

const DOT: Record<RunState, string> = {
  idle: 'bg-slate-500',
  running: 'bg-amber-400 animate-pulse',
  Success: 'bg-green-500',
  Warning: 'bg-amber-400',
  Error: 'bg-red-500',
}

export default function App() {
  const [config, setConfig] = useState<SyncSentinelConfig | null>(null)
  const [tab, setTab] = useState<Tab>('jobs')
  const [editing, setEditing] = useState<Job | null>(null)
  const [runningId, setRunningId] = useState<string | null>(null)
  const logRef = useRef<HTMLPreElement>(null)

  const { connected, run } = useHub(() => setRunningId(null))
  const reload = useCallback(() => api.getConfig().then(setConfig), [])
  useEffect(() => void reload(), [reload])

  useEffect(() => {
    const el = logRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [run.lines])

  async function runJob(job: Job) {
    setRunningId(job.id)
    const res = await api.runJob(job.id)
    if (!res.ok) setRunningId(null) // 409 already running, etc.
  }

  async function deleteJob(job: Job) {
    await api.deleteJob(job.id)
    reload()
  }

  if (!config) return <div className="grid h-screen place-items-center text-slate-500">Loading…</div>

  return (
    <div className="mx-auto flex h-screen max-w-4xl flex-col gap-4 p-6">
      <header className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight">SyncSentinel</h1>
          <nav className="flex gap-1 rounded-lg bg-panel p-1">
            {(['jobs', 'sets', 'settings'] as Tab[]).map((t) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                className={`rounded-md px-3 py-1 text-sm capitalize transition ${tab === t ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                {t === 'sets' ? 'Exclusion sets' : t}
              </button>
            ))}
          </nav>
        </div>
        <span className={`rounded-full px-2.5 py-1 text-xs ${connected ? 'bg-green-500/10 text-green-400' : 'bg-red-500/10 text-red-400'}`}>
          {connected ? 'connected' : 'disconnected'}
        </span>
      </header>

      <div className="min-h-0 flex-1 overflow-auto">
        {tab === 'jobs' && (
          <div className="space-y-3">
            <div className="flex justify-end">
              <button className="btn" onClick={() => setEditing(blankJob())}>+ New job</button>
            </div>
            {config.jobs.length === 0 && <p className="text-sm text-slate-500">No jobs yet — add one.</p>}
            {config.jobs.map((job) => {
              const isRunning = runningId === job.id || (run.state === 'running' && run.jobName === job.name)
              return (
                <div key={job.id} className="flex items-center justify-between rounded-2xl border border-edge bg-panel p-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${isRunning ? DOT.running : job.enabled ? 'bg-slate-500' : 'bg-slate-700'}`} />
                      <span className="truncate font-semibold">{job.name}</span>
                      {!job.enabled && <span className="rounded bg-slate-700/50 px-1.5 text-xs text-slate-400">paused</span>}
                    </div>
                    <div className="mt-1 truncate font-mono text-xs text-slate-400">{job.source} → {job.destination}</div>
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <button className="btn-ghost" disabled={isRunning} onClick={() => runJob(job)}>{isRunning ? 'Running…' : 'Run now'}</button>
                    <button className="btn-ghost" onClick={() => setEditing(job)}>Edit</button>
                    <button className="btn-ghost text-red-400" onClick={() => deleteJob(job)}>Delete</button>
                  </div>
                </div>
              )
            })}
          </div>
        )}

        {tab === 'sets' && <SetsTab folderSets={config.folderSets} fileSets={config.fileSets} onChanged={reload} />}
        {tab === 'settings' && <SettingsTab settings={config.settings} onSaved={reload} />}
      </div>

      {/* Live run panel */}
      <section className="flex h-56 shrink-0 flex-col overflow-hidden rounded-2xl border border-edge bg-panel-2">
        <div className="flex items-center gap-2 border-b border-edge bg-slate-800/30 px-4 py-2.5 text-xs text-slate-400">
          <span className={`h-2 w-2 rounded-full ${DOT[run.state]}`} />
          <span className="font-semibold text-slate-300">{run.jobName ?? 'No run yet'}</span>
          <span>· {run.state}</span>
          {run.exitCode != null && <span className="ml-auto tabular-nums">exit {run.exitCode}</span>}
        </div>
        <pre ref={logRef} className="m-0 flex-1 overflow-auto px-4 py-3 font-mono text-[12px] leading-relaxed break-words whitespace-pre-wrap">
          {run.lines.length === 0 ? <span className="text-slate-600">Run a job to see live output.</span> : run.lines.join('\n')}
        </pre>
      </section>

      {editing && (
        <JobEditor
          job={editing}
          folderSets={config.folderSets}
          fileSets={config.fileSets}
          onSaved={() => { setEditing(null); reload() }}
          onCancel={() => setEditing(null)}
        />
      )}
    </div>
  )
}
