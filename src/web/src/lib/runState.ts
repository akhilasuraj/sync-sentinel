// The live-run model and its transitions, extracted from useHub so the state
// logic is unit-testable without a SignalR connection.

export type RunState = 'idle' | 'running' | 'Success' | 'Warning' | 'Error' | 'Skipped'

export interface RunInfo {
  state: RunState
  jobId: string | null
  jobName: string | null
  exitCode: number | null
  lines: string[]
}

export const IDLE: RunInfo = { state: 'idle', jobId: null, jobName: null, exitCode: null, lines: [] }

/** A run just started: reset to running, clear lines + exit code. */
export const started = (jobId: string, jobName: string): RunInfo => ({ state: 'running', jobId, jobName, exitCode: null, lines: [] })

/** A log line arrived: append it. */
export const logged = (prev: RunInfo, line: string): RunInfo => ({ ...prev, lines: [...prev.lines, line] })

/** The run finished: settle the status + exit code, keep the captured lines. */
export const finished = (prev: RunInfo, status: RunState, exitCode: number): RunInfo => ({ ...prev, state: status, exitCode })
