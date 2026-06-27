import { describe, it, expect } from 'vitest'
import { statusDotClass, runCounts, formatDuration } from './runFormat'

describe('statusDotClass', () => {
  it('maps statuses to colours', () => {
    expect(statusDotClass('Success')).toContain('green')
    expect(statusDotClass('Warning')).toContain('amber')
    expect(statusDotClass('Error')).toContain('red')
  })

  it('maps Skipped (precondition failed, did not run) to yellow', () => {
    expect(statusDotClass('Skipped')).toContain('yellow')
  })

  it('falls back to slate for an unknown status', () => {
    expect(statusDotClass('whatever')).toContain('slate')
  })
})

describe('runCounts', () => {
  it('summarizes copied / extra / failed', () => {
    expect(runCounts({ filesCopied: 4, filesExtra: 1, filesFailed: 0 })).toBe('4 copied · 1 extra · 0 failed')
  })
})

describe('formatDuration', () => {
  it('shows seconds under a minute', () => {
    expect(formatDuration(1.23)).toBe('1.2s')
  })

  it('shows minutes and seconds at or above a minute', () => {
    expect(formatDuration(75)).toBe('1m 15s')
  })
})
