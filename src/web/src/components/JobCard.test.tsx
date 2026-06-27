import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
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
    render(<JobCard job={job()} now={NOW} isRunning={false} onOpen={noop} onRun={noop} />)
    expect(screen.getByText('PEMS')).toBeInTheDocument()
    expect(screen.getByText(/C:\\dev\\PEMS/)).toBeInTheDocument()
  })

  it('marks a disabled job as Paused', () => {
    render(<JobCard job={job({ enabled: false })} now={NOW} isRunning={false} onOpen={noop} onRun={noop} />)
    expect(screen.getByText('Paused')).toBeInTheDocument()
  })

  it('shows the next-run countdown for an enabled idle job', () => {
    render(<JobCard job={job({ enabled: true })} status={status()} now={NOW} isRunning={false} onOpen={noop} onRun={noop} />)
    expect(screen.getByText('next in 4m')).toBeInTheDocument()
  })

  it('disables Run and shows Running… while running', () => {
    render(<JobCard job={job({ enabled: true })} now={NOW} isRunning onOpen={noop} onRun={noop} />)
    expect(screen.getByRole('button', { name: 'Running…' })).toBeDisabled()
  })

  it('calls onRun when Run now is clicked, without opening the job', () => {
    const onRun = vi.fn()
    const onOpen = vi.fn()
    render(<JobCard job={job({ enabled: true })} now={NOW} isRunning={false} onOpen={onOpen} onRun={onRun} />)
    screen.getByRole('button', { name: 'Run now' }).click()
    expect(onRun).toHaveBeenCalledOnce()
    expect(onOpen).not.toHaveBeenCalled()
  })

  it('calls onOpen when the card body is clicked', () => {
    const onOpen = vi.fn()
    render(<JobCard job={job()} now={NOW} isRunning={false} onOpen={onOpen} onRun={noop} />)
    screen.getByText('PEMS').click()
    expect(onOpen).toHaveBeenCalledOnce()
  })

  it('opens on Enter on the card, but Enter on the Run button does not also open', () => {
    const onOpen = vi.fn()
    render(<JobCard job={job()} now={NOW} isRunning={false} onOpen={onOpen} onRun={noop} />)

    // Enter on the nested Run button must not bubble up and open the job.
    fireEvent.keyDown(screen.getByRole('button', { name: 'Run now' }), { key: 'Enter' })
    expect(onOpen).not.toHaveBeenCalled()

    // Enter on the card itself opens it.
    fireEvent.keyDown(screen.getByRole('button', { name: /PEMS/ }), { key: 'Enter' })
    expect(onOpen).toHaveBeenCalledTimes(1)
  })
})
