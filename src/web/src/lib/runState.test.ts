import { describe, it, expect } from 'vitest'
import { IDLE, started, logged, finished } from './runState'

describe('run state transitions', () => {
  it('IDLE is the initial empty state', () => {
    expect(IDLE).toEqual({ state: 'idle', jobId: null, jobName: null, exitCode: null, lines: [] })
  })

  it('started resets to running with the job id + name and no lines', () => {
    const s = started('j1', 'PEMS')
    expect(s.state).toBe('running')
    expect(s.jobId).toBe('j1')
    expect(s.jobName).toBe('PEMS')
    expect(s.exitCode).toBeNull()
    expect(s.lines).toEqual([])
  })

  it('logged appends a line, preserving prior lines and running state', () => {
    const s = logged(logged(started('j1', 'J'), 'a'), 'b')
    expect(s.lines).toEqual(['a', 'b'])
    expect(s.state).toBe('running')
  })

  it('finished settles status + exit code while keeping the captured lines', () => {
    const s = finished(logged(started('j1', 'J'), 'x'), 'Success', 1)
    expect(s.state).toBe('Success')
    expect(s.exitCode).toBe(1)
    expect(s.lines).toEqual(['x'])
  })

  it('finished can settle to Skipped when a precondition failed', () => {
    const s = finished(started('j1', 'J'), 'Skipped', -1)
    expect(s.state).toBe('Skipped')
  })
})
