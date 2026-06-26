import { describe, it, expect } from 'vitest'
import { splitItems, toggleId } from './forms'

describe('splitItems', () => {
  it('splits on commas and whitespace, trimming and dropping empties', () => {
    expect(splitItems('bin, obj,  node_modules')).toEqual(['bin', 'obj', 'node_modules'])
  })

  it('handles newlines and tabs as separators', () => {
    expect(splitItems('*.dll\n*.pdb\t*.exe')).toEqual(['*.dll', '*.pdb', '*.exe'])
  })

  it('returns an empty array for blank input', () => {
    expect(splitItems('   ')).toEqual([])
  })
})

describe('toggleId', () => {
  it('adds an id that is not present', () => {
    expect(toggleId(['a'], 'b')).toEqual(['a', 'b'])
  })

  it('removes an id that is already present', () => {
    expect(toggleId(['a', 'b'], 'a')).toEqual(['b'])
  })
})
