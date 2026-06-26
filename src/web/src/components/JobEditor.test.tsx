import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import JobEditor from './JobEditor'
import { blankJob } from '../types'

// The editor talks to the backend via the api module; mock it so the test
// stays in jsdom (no fetch). The toggle/save *logic* is what we're verifying.
vi.mock('../api', () => ({
  api: {
    preview: vi.fn().mockResolvedValue({ command: 'robocopy ...' }),
    addJob: vi.fn().mockResolvedValue({ id: 'new' }),
    updateJob: vi.fn().mockResolvedValue(undefined),
  },
}))
import { api } from '../api'

const folderSets = [{ id: 'fs1', name: 'DotNet', folders: ['bin', 'obj'] }]

describe('JobEditor', () => {
  beforeEach(() => vi.clearAllMocks())

  it('saves a new job with the toggled folder set attached', async () => {
    const user = userEvent.setup()
    render(<JobEditor job={blankJob()} folderSets={folderSets} fileSets={[]} onSaved={() => {}} onCancel={() => {}} />)

    await user.type(screen.getByLabelText('Name'), 'PEMS')
    await user.type(screen.getByLabelText('Source'), 'C:\\dev\\PEMS')
    await user.type(screen.getByLabelText('Destination'), 'D:\\bak\\PEMS')
    await user.click(screen.getByRole('button', { name: 'DotNet' })) // toggle the set on
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.addJob).toHaveBeenCalledTimes(1)
    expect(api.addJob).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'PEMS', folderSetIds: ['fs1'] }),
    )
  })

  it('keeps Save disabled until name, source and destination are filled', async () => {
    const user = userEvent.setup()
    render(<JobEditor job={blankJob()} folderSets={folderSets} fileSets={[]} onSaved={() => {}} onCancel={() => {}} />)

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    await user.type(screen.getByLabelText('Name'), 'X')
    await user.type(screen.getByLabelText('Source'), 's')
    await user.type(screen.getByLabelText('Destination'), 'd')
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })
})
