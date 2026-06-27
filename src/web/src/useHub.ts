import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { IDLE, started, logged, finished, type RunState } from './lib/runState'

export type { RunState, RunInfo } from './lib/runState'

/**
 * Subscribes to the status hub and tracks the live run via the runState
 * transitions: runStarted resets, log appends, runFinished settles. onFinished
 * fires so callers can clear per-job UI (e.g. the running highlight).
 */
export function useHub(onFinished?: () => void) {
  const [connected, setConnected] = useState(false)
  const [run, setRun] = useState(IDLE)
  const finishedRef = useRef(onFinished)
  finishedRef.current = onFinished

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/status')
      .withAutomaticReconnect()
      .build()

    connection.on('runStarted', (jobId: string, jobName: string) => setRun(started(jobId, jobName)))
    connection.on('log', (line: string) => setRun((r) => logged(r, line)))
    connection.on('runFinished', (status: RunState, exitCode: number) => {
      setRun((r) => finished(r, status, exitCode))
      finishedRef.current?.()
    })

    connection.start().then(() => setConnected(true)).catch(() => setConnected(false))
    return () => void connection.stop()
  }, [])

  return { connected, run }
}
