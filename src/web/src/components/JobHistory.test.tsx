import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import JobHistory from './JobHistory'

// The factory is hoisted above module-level consts, so the run fixture is
// defined inline here rather than referencing an outer variable.
vi.mock('../api', () => ({
  api: {
    getRuns: vi.fn().mockResolvedValue([
      {
        id: 'r1', jobId: 'j1', jobName: 'PEMS', status: 'Success',
        startedUtc: '2026-06-26T12:00:00Z', finishedUtc: '2026-06-26T12:00:05Z',
        filesCopied: 4, filesSkipped: 0, filesFailed: 0, filesExtra: 1,
        exitCode: 1, logPath: 'x.log', durationSeconds: 5,
      },
    ]),
    getRunLog: vi.fn().mockResolvedValue('robocopy log contents here'),
  },
}))
import { api } from '../api'

describe('JobHistory', () => {
  beforeEach(() => vi.clearAllMocks())

  it('lists the job runs with their counts', async () => {
    render(<JobHistory jobId="j1" jobName="PEMS" onClose={() => {}} />)

    expect(await screen.findByText(/4 copied · 1 extra · 0 failed/)).toBeInTheDocument()
    expect(api.getRuns).toHaveBeenCalledWith('j1')
  })

  it('loads and shows the log when a run is clicked', async () => {
    const user = userEvent.setup()
    render(<JobHistory jobId="j1" jobName="PEMS" onClose={() => {}} />)

    await user.click(await screen.findByText(/4 copied/))

    expect(api.getRunLog).toHaveBeenCalledWith('r1')
    expect(await screen.findByText(/robocopy log contents here/)).toBeInTheDocument()
  })
})
