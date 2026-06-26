import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'

export type RunState = 'idle' | 'running' | 'Success' | 'Warning' | 'Error'

export interface RunInfo {
  state: RunState
  jobName: string | null
  exitCode: number | null
  lines: string[]
}

const IDLE: RunInfo = { state: 'idle', jobName: null, exitCode: null, lines: [] }

/**
 * Subscribes to the status hub and tracks the live run: runStarted resets the
 * panel, log appends lines, runFinished settles the status. onFinished fires so
 * callers can clear per-job UI (e.g. the running highlight).
 */
export function useHub(onFinished?: () => void) {
  const [connected, setConnected] = useState(false)
  const [run, setRun] = useState<RunInfo>(IDLE)
  const finishedRef = useRef(onFinished)
  finishedRef.current = onFinished

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/status')
      .withAutomaticReconnect()
      .build()

    connection.on('runStarted', (jobName: string) =>
      setRun({ state: 'running', jobName, exitCode: null, lines: [] }),
    )
    connection.on('log', (line: string) =>
      setRun((r) => ({ ...r, lines: [...r.lines, line] })),
    )
    connection.on('runFinished', (status: RunState, exitCode: number) => {
      setRun((r) => ({ ...r, state: status, exitCode }))
      finishedRef.current?.()
    })

    connection.start().then(() => setConnected(true)).catch(() => setConnected(false))
    return () => void connection.stop()
  }, [])

  return { connected, run }
}
