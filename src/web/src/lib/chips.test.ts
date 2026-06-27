import { describe, it, expect } from 'vitest'
import { addChips } from './chips'

describe('addChips', () => {
  it('appends parsed items (comma/space separated)', () => {
    expect(addChips(['bin', 'obj'], 'node_modules, dist')).toEqual(['bin', 'obj', 'node_modules', 'dist'])
  })

  it('de-duplicates case-insensitively, keeping the existing entry', () => {
    expect(addChips(['bin'], 'BIN obj')).toEqual(['bin', 'obj'])
  })

  it('returns the list unchanged for blank input', () => {
    expect(addChips(['bin'], '   ')).toEqual(['bin'])
  })
})
