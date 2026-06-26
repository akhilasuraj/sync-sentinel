import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import JobCard from './JobCard'
import { blankJob, type Job } from '../types'

const job = (over: Partial<Job> = {}): Job => ({
  ...blankJob(),
  id: 'j1',
  name: 'PEMS',
  source: 'C:\\dev\\PEMS',
  destination: 'D:\\bak\\PEMS',
  ...over,
})

const noop = () => {}

describe('JobCard', () => {
  it('shows the job name and source → destination', () => {
    render(<JobCard job={job()} isRunning={false} onRun={noop} onEdit={noop} onDelete={noop} />)
    expect(screen.getByText('PEMS')).toBeInTheDocument()
    expect(screen.getByText(/C:\\dev\\PEMS/)).toBeInTheDocument()
  })

  it('marks a disabled job as paused', () => {
    render(<JobCard job={job({ enabled: false })} isRunning={false} onRun={noop} onEdit={noop} onDelete={noop} />)
    expect(screen.getByText('paused')).toBeInTheDocument()
  })

  it('disables Run and shows Running… while running', () => {
    render(<JobCard job={job({ enabled: true })} isRunning onRun={noop} onEdit={noop} onDelete={noop} />)
    expect(screen.getByRole('button', { name: 'Running…' })).toBeDisabled()
  })

  it('calls onRun when Run now is clicked', () => {
    const onRun = vi.fn()
    render(<JobCard job={job({ enabled: true })} isRunning={false} onRun={onRun} onEdit={noop} onDelete={noop} />)
    screen.getByRole('button', { name: 'Run now' }).click()
    expect(onRun).toHaveBeenCalledOnce()
  })
})
