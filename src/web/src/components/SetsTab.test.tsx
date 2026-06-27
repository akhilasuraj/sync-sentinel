import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import SetsTab from './SetsTab'

vi.mock('../api', () => ({
  api: {
    addFolderSet: vi.fn().mockResolvedValue({}),
    updateFolderSet: vi.fn().mockResolvedValue(undefined),
    deleteFolderSet: vi.fn().mockResolvedValue(undefined),
    addFileSet: vi.fn().mockResolvedValue({}),
    updateFileSet: vi.fn().mockResolvedValue(undefined),
    deleteFileSet: vi.fn().mockResolvedValue(undefined),
  },
}))
import { api } from '../api'

const folderSets = [{ id: 's1', name: 'Dev', folders: ['bin', 'obj'] }]
const render_ = () => render(<SetsTab folderSets={folderSets} fileSets={[]} onChanged={() => {}} />)

describe('SetsTab editor', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows items as chips and is not dirty initially', () => {
    render_()
    expect(screen.getByText('bin')).toBeInTheDocument()
    expect(screen.getByText('obj')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
  })

  it('removing a chip stages the change and Save persists it', async () => {
    const user = userEvent.setup()
    render_()

    await user.click(screen.getByRole('button', { name: 'Remove obj' }))
    expect(screen.queryByText('obj')).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(api.updateFolderSet).toHaveBeenCalledWith('s1', { id: 's1', name: 'Dev', folders: ['bin'] })
  })

  it('Cancel reverts a staged change without saving', async () => {
    const user = userEvent.setup()
    render_()

    await user.click(screen.getByRole('button', { name: 'Remove obj' }))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByText('obj')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
    expect(api.updateFolderSet).not.toHaveBeenCalled()
  })
})
