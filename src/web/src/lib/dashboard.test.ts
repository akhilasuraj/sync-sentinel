import { describe, it, expect } from 'vitest'
import { fleetSummary, nextUp } from './dashboard'
import { blankJob, type Job } from '../types'
import type { JobStatus } from './jobStatus'

const job = (id: string, over: Partial<Job> = {}): Job => ({ ...blankJob(), id, name: id, source: 's', destination: 'd', ...over })
const st = (over: Partial<JobStatus> = {}): JobStatus => ({ jobId: '', lastStatus: null, nextDueUtc: null, state: 'Idle', ...over })

describe('fleetSummary', () => {
  it('counts total, watching (enabled), paused, and attention (Error/Skipped last run)', () => {
    const jobs = [job('a', { enabled: true }), job('b', { enabled: false }), job('c', { enabled: true })]
    const statuses = { a: st({ lastStatus: 'Success' }), b: st({ lastStatus: 'Error' }), c: st({ lastStatus: 'Skipped' }) }

    expect(fleetSummary(jobs, statuses)).toEqual({ total: 3, watching: 2, paused: 1, attention: 2 })
  })
})

describe('nextUp', () => {
  const now = Date.parse('2026-06-27T12:00:00Z')

  it('features the running job over any scheduled one', () => {
    const jobs = [job('a', { enabled: true }), job('b', { enabled: true })]
    const statuses = {
      a: st({ jobId: 'a', nextDueUtc: '2026-06-27T12:01:00Z' }),
      b: st({ jobId: 'b', state: 'Running' }),
    }
    expect(nextUp(jobs, statuses, now)?.job.id).toBe('b')
  })

  it('otherwise features the enabled job due soonest', () => {
    const jobs = [job('a', { enabled: true }), job('b', { enabled: true }), job('c', { enabled: false })]
    const statuses = {
      a: st({ jobId: 'a', nextDueUtc: '2026-06-27T12:30:00Z' }),
      b: st({ jobId: 'b', nextDueUtc: '2026-06-27T12:05:00Z' }),
      c: st({ jobId: 'c', nextDueUtc: '2026-06-27T12:01:00Z' }), // paused — ignored
    }
    expect(nextUp(jobs, statuses, now)?.job.id).toBe('b')
  })

  it('returns null when nothing is running or scheduled', () => {
    const jobs = [job('a', { enabled: false })]
    expect(nextUp(jobs, { a: st({ jobId: 'a' }) }, now)).toBeNull()
  })
})
