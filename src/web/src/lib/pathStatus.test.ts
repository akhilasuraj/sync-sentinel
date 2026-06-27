import { describe, it, expect } from 'vitest'
import { pathHint } from './pathStatus'

describe('pathHint', () => {
  it('a source that exists is OK', () => {
    expect(pathHint('source', true)).toEqual({ tone: 'ok', text: 'Folder exists' })
  })

  it('a missing source warns — you can’t back up a folder that isn’t there', () => {
    expect(pathHint('source', false)).toEqual({ tone: 'warn', text: 'Folder not found' })
  })

  it('a missing destination is informational — robocopy creates it', () => {
    expect(pathHint('destination', false)).toEqual({ tone: 'info', text: 'Will be created on first run' })
  })

  it('a destination that exists is OK', () => {
    expect(pathHint('destination', true)).toEqual({ tone: 'ok', text: 'Folder exists' })
  })
})
