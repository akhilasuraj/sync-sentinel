import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import SetSelector from './SetSelector'

const sets = [{ id: 'a', name: 'Alpha' }, { id: 'b', name: 'Beta' }]

describe('SetSelector', () => {
  it('lists selected sets as removable and the rest as addable', () => {
    render(<SetSelector label="Folder sets" sets={sets} selectedIds={['a']} onChange={() => {}} emptyLabel="none" />)
    expect(screen.getByRole('button', { name: 'Remove Alpha' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Beta' })).toBeInTheDocument()
  })

  it('adds a set when its "+" chip is clicked', () => {
    const onChange = vi.fn()
    render(<SetSelector label="x" sets={sets} selectedIds={['a']} onChange={onChange} emptyLabel="none" />)
    screen.getByRole('button', { name: 'Add Beta' }).click()
    expect(onChange).toHaveBeenCalledWith(['a', 'b'])
  })

  it('removes a set when its × is clicked', () => {
    const onChange = vi.fn()
    render(<SetSelector label="x" sets={sets} selectedIds={['a', 'b']} onChange={onChange} emptyLabel="none" />)
    screen.getByRole('button', { name: 'Remove Alpha' }).click()
    expect(onChange).toHaveBeenCalledWith(['b'])
  })

  it('shows the empty label when no sets exist', () => {
    render(<SetSelector label="x" sets={[]} selectedIds={[]} onChange={() => {}} emptyLabel="No sets yet" />)
    expect(screen.getByText('No sets yet')).toBeInTheDocument()
  })
})
