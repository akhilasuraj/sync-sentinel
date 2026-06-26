import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'

type HubState = 'connecting' | 'connected' | 'error'

export default function App() {
  const [ping, setPing] = useState<string>('…')
  const [tick, setTick] = useState<number | null>(null)
  const [hub, setHub] = useState<HubState>('connecting')

  useEffect(() => {
    fetch('/api/ping')
      .then((r) => r.json())
      .then((d) => setPing(d.message))
      .catch(() => setPing('error'))

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/status')
      .withAutomaticReconnect()
      .build()

    connection.on('tick', (n: number) => setTick(n))
    connection
      .start()
      .then(() => setHub('connected'))
      .catch(() => setHub('error'))

    return () => void connection.stop()
  }, [])

  return (
    <main className="app">
      <div className="card">
        <h1>SyncSentinel</h1>
        <p className="subtitle">Phase 0 — walking skeleton</p>
        <ul className="checks">
          <li>
            <span>
              REST <code>/api/ping</code>
            </span>
            <strong className={ping === 'pong' ? 'ok' : ''}>{ping}</strong>
          </li>
          <li>
            <span>SignalR hub</span>
            <strong className={hub === 'connected' ? 'ok' : hub === 'error' ? 'bad' : ''}>{hub}</strong>
          </li>
          <li>
            <span>Live heartbeat</span>
            <strong className={tick != null ? 'ok' : ''}>{tick != null ? `tick #${tick}` : 'waiting…'}</strong>
          </li>
        </ul>
      </div>
    </main>
  )
}
