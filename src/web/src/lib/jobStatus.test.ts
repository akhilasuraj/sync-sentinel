import { describe, it, expect } from 'vitest'
import { cardState, clockCountdown, formatCountdown, type JobStatus } from './jobStatus'

describe('formatCountdown', () => {
  const now = Date.parse('2026-06-27T12:00:00Z')

  it('says "Due now" when the next run is already due', () => {
    expect(formatCountdown('2026-06-27T11:59:00Z', now)).toBe('Due now')
  })

  it('counts down in whole minutes under an hour', () => {
    expect(formatCountdown('2026-06-27T12:04:00Z', now)).toBe('next in 4m')
  })

  it('counts down in seconds under a minute', () => {
    expect(formatCountdown('2026-06-27T12:00:45Z', now)).toBe('next in 45s')
  })

  it('counts down in hours and minutes past an hour', () => {
    expect(formatCountdown('2026-06-27T13:05:00Z', now)).toBe('next in 1h 5m')
  })

  it('counts down in days and hours past a day', () => {
    expect(formatCountdown('2026-06-29T15:00:00Z', now)).toBe('next in 2d 3h')
  })
})

describe('clockCountdown', () => {
  const now = Date.parse('2026-06-27T12:00:00Z')

  it('shows MM:SS under an hour', () => {
    expect(clockCountdown('2026-06-27T12:04:12Z', now)).toBe('04:12')
  })

  it('shows H:MM:SS at or past an hour', () => {
    expect(clockCountdown('2026-06-27T13:04:05Z', now)).toBe('1:04:05')
  })

  it('shows "Due" once the run is due', () => {
    expect(clockCountdown('2026-06-27T11:59:00Z', now)).toBe('Due')
  })
})

describe('cardState', () => {
  const now = Date.parse('2026-06-27T12:00:00Z')
  const base: JobStatus = { jobId: 'j', lastStatus: 'Success', nextDueUtc: '2026-06-27T12:04:00Z', state: 'Idle' }

  it('shows a pulsing amber dot and "Running…" while running', () => {
    const r = cardState({ ...base, state: 'Running' }, true, now)
    expect(r.label).toBe('Running…')
    expect(r.dot).toContain('amber')
    expect(r.dot).toContain('animate-pulse')
  })

  it('shows a pulsing slate dot and "Queued" while queued', () => {
    const r = cardState({ ...base, state: 'Queued' }, true, now)
    expect(r.label).toBe('Queued')
    expect(r.dot).toContain('slate')
    expect(r.dot).toContain('animate-pulse')
  })

  it('for an idle enabled job, colours the dot by last status and labels with the countdown', () => {
    const ok = cardState({ ...base, lastStatus: 'Success' }, true, now)
    expect(ok.dot).toContain('green')
    expect(ok.label).toBe('next in 4m')
    expect(cardState({ ...base, lastStatus: 'Error' }, true, now).dot).toContain('red')
  })

  it('shows a muted dot and "Paused" for a disabled job, regardless of last status', () => {
    const r = cardState({ ...base, lastStatus: 'Success' }, false, now)
    expect(r.label).toBe('Paused')
    expect(r.dot).toContain('slate')
    expect(r.dot).not.toContain('animate-pulse')
  })

  it('colours a Skipped last run yellow and keeps the countdown label', () => {
    const r = cardState({ ...base, lastStatus: 'Skipped' }, true, now)
    expect(r.dot).toContain('yellow')
    expect(r.label).toBe('next in 4m')
  })

  it('shows a grey dot and "Not run yet" for an enabled job that never ran', () => {
    const r = cardState({ ...base, lastStatus: null, nextDueUtc: null }, true, now)
    expect(r.label).toBe('Not run yet')
    expect(r.dot).toContain('slate')
  })
})
