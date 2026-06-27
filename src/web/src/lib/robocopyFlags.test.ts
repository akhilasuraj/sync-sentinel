import { describe, it, expect } from 'vitest'
import { addFlag, lookupFlag, parseFlags, removeFlag, serializeFlags, updateValue } from './robocopyFlags'

describe('parse / serialize', () => {
  it('parses toggle and value flags', () => {
    expect(parseFlags('/MIR /R:3 /W:5')).toEqual([
      { name: '/MIR', value: null },
      { name: '/R', value: '3' },
      { name: '/W', value: '5' },
    ])
  })

  it('ignores extra whitespace and blank input', () => {
    expect(parseFlags('   /MIR   /XJ ')).toEqual([{ name: '/MIR', value: null }, { name: '/XJ', value: null }])
    expect(parseFlags('   ')).toEqual([])
  })

  it('round-trips through serialize', () => {
    expect(serializeFlags(parseFlags('/MIR /R:3 /FFT'))).toBe('/MIR /R:3 /FFT')
  })
})

describe('editing', () => {
  it('addFlag appends a parsed token', () => {
    expect(addFlag([{ name: '/MIR', value: null }], '/R:3')).toEqual([
      { name: '/MIR', value: null },
      { name: '/R', value: '3' },
    ])
  })

  it('addFlag ignores a flag already present (case-insensitive) or blank input', () => {
    const chips = [{ name: '/MIR', value: null }]
    expect(addFlag(chips, '/mir')).toEqual(chips)
    expect(addFlag(chips, '   ')).toEqual(chips)
  })

  it('updateValue changes a value flag in place', () => {
    expect(updateValue([{ name: '/R', value: '3' }], 0, '5')).toEqual([{ name: '/R', value: '5' }])
  })

  it('removeFlag drops the flag at the index', () => {
    expect(removeFlag([{ name: '/MIR', value: null }, { name: '/R', value: '3' }], 0)).toEqual([{ name: '/R', value: '3' }])
  })
})

describe('catalog', () => {
  it('looks up a flag case-insensitively and exposes whether it takes a value', () => {
    expect(lookupFlag('/mir')?.label).toBe('Mirror')
    expect(lookupFlag('/R')?.takesValue).toBe(true)
    expect(lookupFlag('/NOPE')).toBeUndefined()
  })
})
