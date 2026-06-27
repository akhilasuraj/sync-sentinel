import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { useHub } from './useHub'
import { blankJob, type Job, type RunRecord, type RunStats, type SyncSentinelConfig } from './types'
import type { JobStatus } from './lib/jobStatus'
import Sidebar, { type Route } from './components/Sidebar'
import Dashboard from './components/Dashboard'
import JobCard from './components/JobCard'
import JobEditor from './components/JobEditor'
import JobHistory from './components/JobHistory'
import SetsTab from './components/SetsTab'
import SettingsTab from './components/SettingsTab'

export default function App() {
  const [config, setConfig] = useState<SyncSentinelConfig | null>(null)
  const [route, setRoute] = useState<Route>('dashboard')
  const [editing, setEditing] = useState<Job | null>(null)
  const [historyJob, setHistoryJob] = useState<Job | null>(null)
  const [runningId, setRunningId] = useState<string | null>(null)
  const [statuses, setStatuses] = useState<Record<string, JobStatus>>({})
  const [recent, setRecent] = useState<RunRecord[]>([])
  const [stats, setStats] = useState<RunStats | null>(null)
  const [now, setNow] = useState(() => Date.now())

  const { connected, run } = useHub(() => setRunningId(null))
  const reload = useCallback(() => api.getConfig().then(setConfig), [])
  const reloadStatuses = useCallback(
    () => api.getJobStatuses().then((list) => setStatuses(Object.fromEntries(list.map((s) => [s.jobId, s])))),
    [],
  )
  const reloadDashboard = useCallback(() => {
    api.getRecentRuns(10).then(setRecent).catch(() => {})
    api.getStats().then(setStats).catch(() => {})
  }, [])

  useEffect(() => void reload(), [reload])

  // Refresh the run-state feed + dashboard data on load and on every run change.
  useEffect(() => {
    reloadStatuses()
    reloadDashboard()
  }, [reloadStatuses, reloadDashboard, run.state])

  // Tick a shared clock so countdowns stay live.
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(t)
  }, [])

  async function runJob(job: Job) {
    setRunningId(job.id)
    const res = await api.runJob(job.id)
    if (!res.ok) setRunningId(null) // 409 already running / 422 precondition failed
  }

  async function deleteJob(job: Job) {
    await api.deleteJob(job.id)
    reload()
  }

  if (!config) return <div className="grid h-screen place-items-center text-slate-500">Loading…</div>

  return (
    <div className="flex h-screen">
      <Sidebar route={route} onNavigate={setRoute} connected={connected} />

      <main className="min-w-0 flex-1 overflow-auto">
        {route === 'dashboard' && (
          <Dashboard
            jobs={config.jobs}
            statusById={statuses}
            now={now}
            recent={recent}
            stats={stats}
            onOpenJob={() => setRoute('jobs')}
            onCreate={() => setEditing(blankJob())}
          />
        )}

        {route === 'jobs' && (
          <div className="mx-auto max-w-5xl px-8 py-8">
            <div className="flex items-center justify-between">
              <p className="eyebrow">Jobs</p>
              <button className="btn" onClick={() => setEditing(blankJob())}>+ New job</button>
            </div>

            {config.jobs.length === 0 ? (
              <div className="mt-6 rounded-2xl border border-dashed border-edge bg-panel/50 p-12 text-center">
                <h2 className="text-lg font-semibold text-slate-100">No jobs yet</h2>
                <p className="mx-auto mt-1 max-w-sm text-sm text-slate-500">
                  Back up your first folder in seconds — choose a source and a destination and SyncSentinel keeps them mirrored.
                </p>
                <button className="btn mt-5" onClick={() => setEditing(blankJob())}>Create a job</button>
              </div>
            ) : (
              <div className="mt-5 space-y-3">
                {config.jobs.map((job) => (
                  <JobCard
                    key={job.id}
                    job={job}
                    status={statuses[job.id]}
                    now={now}
                    isRunning={runningId === job.id || (run.state === 'running' && run.jobName === job.name)}
                    onRun={() => runJob(job)}
                    onEdit={() => setEditing(job)}
                    onDelete={() => deleteJob(job)}
                    onHistory={() => setHistoryJob(job)}
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {route === 'sets' && (
          <div className="mx-auto max-w-5xl px-8 py-8">
            <p className="eyebrow mb-5">Exclusion sets</p>
            <SetsTab folderSets={config.folderSets} fileSets={config.fileSets} onChanged={reload} />
          </div>
        )}

        {route === 'settings' && (
          <div className="mx-auto max-w-5xl px-8 py-8">
            <p className="eyebrow mb-5">Settings</p>
            <SettingsTab settings={config.settings} onSaved={reload} />
          </div>
        )}
      </main>

      {editing && (
        <JobEditor
          job={editing}
          folderSets={config.folderSets}
          fileSets={config.fileSets}
          onSaved={() => { setEditing(null); reload() }}
          onCancel={() => setEditing(null)}
        />
      )}

      {historyJob && (
        <JobHistory jobId={historyJob.id} jobName={historyJob.name} onClose={() => setHistoryJob(null)} />
      )}
    </div>
  )
}
