import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'

type RunState = 'idle' | 'running' | 'Success' | 'Warning' | 'Error'

const STATUS_LABEL: Record<RunState, string> = {
  idle: 'Idle',
  running: 'Running…',
  Success: 'Success',
  Warning: 'Warning',
  Error: 'Error',
}

const DOT_COLOR: Record<RunState, string> = {
  idle: 'bg-slate-500',
  running: 'bg-amber-400 animate-pulse',
  Success: 'bg-green-500',
  Warning: 'bg-amber-400',
  Error: 'bg-red-500',
}

export default function App() {
  const [hubReady, setHubReady] = useState(false)
  const [state, setState] = useState<RunState>('idle')
  const [jobName, setJobName] = useState<string | null>(null)
  const [exitCode, setExitCode] = useState<number | null>(null)
  const [lines, setLines] = useState<string[]>([])
  const [busy, setBusy] = useState(false)
  const logRef = useRef<HTMLPreElement>(null)

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/status')
      .withAutomaticReconnect()
      .build()

    connection.on('runStarted', (name: string) => {
      setJobName(name)
      setExitCode(null)
      setLines([])
      setState('running')
    })
    connection.on('log', (line: string) => {
      setLines((prev) => [...prev, line])
    })
    connection.on('runFinished', (status: RunState, code: number) => {
      setState(status)
      setExitCode(code)
      setBusy(false)
    })

    connection
      .start()
      .then(() => setHubReady(true))
      .catch(() => setHubReady(false))

    return () => void connection.stop()
  }, [])

  // Keep the log scrolled to the newest line.
  useEffect(() => {
    const el = logRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [lines])

  async function runNow() {
    setBusy(true)
    const res = await fetch('/api/run', { method: 'POST' })
    if (!res.ok) setBusy(false) // 409 = a run is already in progress
  }

  const running = state === 'running'

  return (
    <main className="mx-auto flex h-screen max-w-3xl flex-col gap-4 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold tracking-tight">SyncSentinel</h1>
        <span
          className={`rounded-full px-2.5 py-1 text-xs ${
            hubReady ? 'bg-green-500/10 text-green-400' : 'bg-red-500/10 text-red-400'
          }`}
        >
          {hubReady ? 'connected' : 'disconnected'}
        </span>
      </header>

      <section className="rounded-2xl border border-edge bg-panel p-5">
        <div className="flex items-center justify-between gap-4">
          <div>
            <div className="font-semibold">{jobName ?? 'Demo backup job'}</div>
            <div className="mt-0.5 text-sm text-slate-400">robocopy mirror · excludes bin</div>
          </div>
          <button
            onClick={runNow}
            disabled={busy || running || !hubReady}
            className="rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-default disabled:bg-slate-700 disabled:text-slate-500"
          >
            {running ? 'Running…' : 'Run now'}
          </button>
        </div>
        <div className="mt-4 flex items-center gap-2.5 border-t border-edge pt-4">
          <span className={`h-2.5 w-2.5 rounded-full ${DOT_COLOR[state]}`} />
          <span className="font-semibold">{STATUS_LABEL[state]}</span>
          {exitCode != null && <span className="ml-auto text-xs tabular-nums text-slate-400">exit {exitCode}</span>}
        </div>
      </section>

      <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-2xl border border-edge bg-panel-2">
        <div className="border-b border-edge bg-slate-800/30 px-4 py-2.5 text-xs text-slate-400">
          Live log{lines.length > 0 && <span> · {lines.length} lines</span>}
        </div>
        <pre
          ref={logRef}
          className="m-0 flex-1 overflow-auto px-4 py-3.5 font-mono text-[12.5px] leading-relaxed break-words whitespace-pre-wrap"
        >
          {lines.length === 0 ? <span className="text-slate-600">No output yet — press Run now.</span> : lines.join('\n')}
        </pre>
      </section>
    </main>
  )
}
