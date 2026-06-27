import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FlagsEditor from './FlagsEditor'

describe('FlagsEditor', () => {
  it('renders a chip per flag, with an inline value for value flags', () => {
    render(<FlagsEditor value="/MIR /R:3" onChange={() => {}} />)
    expect(screen.getByText('/MIR')).toBeInTheDocument()
    expect(screen.getByText('/R')).toBeInTheDocument()
    expect(screen.getByLabelText('/R value')).toHaveValue('3')
  })

  it('emits the remaining flags when a chip is removed', async () => {
    const onChange = vi.fn()
    render(<FlagsEditor value="/MIR /R:3" onChange={onChange} />)
    await userEvent.click(screen.getByRole('button', { name: 'Remove /MIR' }))
    expect(onChange).toHaveBeenCalledWith('/R:3')
  })

  it('emits the new value when a value flag is edited', () => {
    const onChange = vi.fn()
    render(<FlagsEditor value="/MIR /R:3" onChange={onChange} />)
    fireEvent.change(screen.getByLabelText('/R value'), { target: { value: '5' } })
    expect(onChange).toHaveBeenCalledWith('/MIR /R:5')
  })

  it('appends a custom flag', async () => {
    const onChange = vi.fn()
    render(<FlagsEditor value="/MIR" onChange={onChange} />)
    await userEvent.type(screen.getByLabelText('Custom flag'), '/Z')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))
    expect(onChange).toHaveBeenCalledWith('/MIR /Z')
  })

  it('adds a flag chosen from the catalog dropdown with its default value', async () => {
    const onChange = vi.fn()
    render(<FlagsEditor value="/MIR" onChange={onChange} />)
    await userEvent.click(screen.getByRole('button', { name: '+ Add flag' }))
    await userEvent.click(screen.getByText('Retries')) // the /R catalog entry
    expect(onChange).toHaveBeenCalledWith('/MIR /R:3')
  })
})
