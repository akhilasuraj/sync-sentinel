import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import JobCard from './JobCard'
import { blankJob, type Job } from '../types'
import type { JobStatus } from '../lib/jobStatus'

const job = (over: Partial<Job> = {}): Job => ({
  ...blankJob(),
  id: 'j1',
  name: 'PEMS',
  source: 'C:\\dev\\PEMS',
  destination: 'D:\\bak\\PEMS',
  ...over,
})

const NOW = Date.parse('2026-06-27T12:00:00Z')
const status = (over: Partial<JobStatus> = {}): JobStatus => ({
  jobId: 'j1',
  lastStatus: 'Success',
  nextDueUtc: '2026-06-27T12:04:00Z',
  state: 'Idle',
  ...over,
})
const noop = () => {}

describe('JobCard', () => {
  it('shows the job name and source → destination', () => {
    render(<JobCard job={job()} now={NOW} isRunning={false} onRun={noop} onEdit={noop} onDelete={noop} onHistory={noop} />)
    expect(screen.getByText('PEMS')).toBeInTheDocument()
    expect(screen.getByText(/C:\\dev\\PEMS/)).toBeInTheDocument()
  })

  it('marks a disabled job as Paused', () => {
    render(<JobCard job={job({ enabled: false })} now={NOW} isRunning={false} onRun={noop} onEdit={noop} onDelete={noop} onHistory={noop} />)
    expect(screen.getByText('Paused')).toBeInTheDocument()
  })

  it('shows the next-run countdown for an enabled idle job', () => {
    render(<JobCard job={job({ enabled: true })} status={status()} now={NOW} isRunning={false} onRun={noop} onEdit={noop} onDelete={noop} onHistory={noop} />)
    expect(screen.getByText('next in 4m')).toBeInTheDocument()
  })

  it('disables Run and shows Running… while running', () => {
    render(<JobCard job={job({ enabled: true })} now={NOW} isRunning onRun={noop} onEdit={noop} onDelete={noop} onHistory={noop} />)
    expect(screen.getByRole('button', { name: 'Running…' })).toBeDisabled()
  })

  it('calls onRun when Run now is clicked', () => {
    const onRun = vi.fn()
    render(<JobCard job={job({ enabled: true })} now={NOW} isRunning={false} onRun={onRun} onEdit={noop} onDelete={noop} onHistory={noop} />)
    screen.getByRole('button', { name: 'Run now' }).click()
    expect(onRun).toHaveBeenCalledOnce()
  })
})
