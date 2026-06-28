import { useEffect, useState } from 'react'
import { useAppData } from './useAppData'
import { blankJob, type Job } from './types'
import Sidebar, { type Route } from './components/Sidebar'
import Dashboard from './components/Dashboard'
import JobCard from './components/JobCard'
import JobDetails from './components/JobDetails'
import JobEditor from './components/JobEditor'
import SetsTab from './components/SetsTab'
import SettingsTab from './components/SettingsTab'

export default function App() {
  const { config, statuses, recent, stats, connected, run, isJobRunning, runJob, deleteJob, refresh } = useAppData()
  const [route, setRoute] = useState<Route>('dashboard')
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null)
  const [editing, setEditing] = useState<Job | null>(null)
  const [now, setNow] = useState(() => Date.now())

  // Tick a shared clock so countdowns stay live.
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(t)
  }, [])

  async function removeJob(job: Job) {
    await deleteJob(job.id)
    setSelectedJobId(null)
  }

  if (!config) return <div className="grid h-screen place-items-center text-slate-500">Loading…</div>

  const selectedJob = selectedJobId ? config.jobs.find((j) => j.id === selectedJobId) : undefined

  return (
    <div className="flex h-screen">
      <Sidebar route={route} onNavigate={(r) => { setRoute(r); setSelectedJobId(null) }} connected={connected} />

      <main className="min-w-0 flex-1 overflow-auto">
        {route === 'dashboard' && (
          <Dashboard
            jobs={config.jobs}
            statusById={statuses}
            now={now}
            recent={recent}
            stats={stats}
            onOpenJob={(id) => { setRoute('jobs'); setSelectedJobId(id) }}
            onCreate={() => setEditing(blankJob())}
          />
        )}

        {route === 'jobs' && selectedJob && (
          <JobDetails
            job={selectedJob}
            status={statuses[selectedJob.id]}
            now={now}
            run={run}
            isRunning={isJobRunning(selectedJob.id)}
            onBack={() => setSelectedJobId(null)}
            onRun={() => runJob(selectedJob.id)}
            onEdit={() => setEditing(selectedJob)}
            onDelete={() => removeJob(selectedJob)}
          />
        )}

        {route === 'jobs' && !selectedJob && (
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
                    isRunning={isJobRunning(job.id)}
                    onOpen={() => setSelectedJobId(job.id)}
                    onRun={() => runJob(job.id)}
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {route === 'sets' && (
          <div className="mx-auto max-w-5xl px-8 py-8">
            <p className="eyebrow mb-5">Exclusion sets</p>
            <SetsTab folderSets={config.folderSets} fileSets={config.fileSets} onChanged={refresh} />
          </div>
        )}

        {route === 'settings' && (
          <div className="mx-auto max-w-5xl px-8 py-8">
            <p className="eyebrow mb-5">Settings</p>
            <SettingsTab settings={config.settings} onSaved={refresh} />
          </div>
        )}
      </main>

      {editing && (
        <JobEditor
          job={editing}
          folderSets={config.folderSets}
          fileSets={config.fileSets}
          onSaved={() => { setEditing(null); refresh() }}
          onCancel={() => setEditing(null)}
        />
      )}
    </div>
  )
}
