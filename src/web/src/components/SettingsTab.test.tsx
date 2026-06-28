import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import SettingsTab from './SettingsTab'
import type { GlobalSettings } from '../types'

vi.mock('../api', () => ({
  api: {
    updateSettings: vi.fn().mockResolvedValue(undefined),
    capabilities: vi.fn().mockResolvedValue({ folderPicker: false }),
    wipeData: vi.fn().mockResolvedValue({ ok: true }),
  },
}))
import { api } from '../api'

const settings: GlobalSettings = {
  defaultFlags: '/MIR',
  defaultIntervalMinutes: 15,
  maxConcurrent: 1,
  retention: { runsPerJob: 100, days: 30 },
  autostart: true,
}

describe('SettingsTab', () => {
  beforeEach(() => vi.clearAllMocks())

  it('disables Save and hides Cancel until something changes', () => {
    render(<SettingsTab settings={settings} onSaved={() => {}} />)
    expect(screen.getByRole('button', { name: 'Save settings' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it('shows Cancel on change, and Cancel reverts the edit', () => {
    render(<SettingsTab settings={settings} onSaved={() => {}} />)
    const maxConcurrent = screen.getByLabelText('Max concurrent runs')

    fireEvent.change(maxConcurrent, { target: { value: '3' } })
    expect(screen.getByRole('button', { name: 'Save settings' })).toBeEnabled()
    const cancel = screen.getByRole('button', { name: 'Cancel' })

    fireEvent.click(cancel)
    expect(maxConcurrent).toHaveValue(1)
    expect(screen.queryByRole('button', { name: 'Cancel' })).toBeNull()
  })

  it('hides the danger zone when not running in the desktop shell', async () => {
    render(<SettingsTab settings={settings} onSaved={() => {}} />)
    // capabilities resolves folderPicker:false → no wipe action
    expect(await screen.findByRole('button', { name: 'Save settings' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Remove all data/ })).toBeNull()
  })

  it('in the shell, confirming the danger action wipes the data', async () => {
    vi.mocked(api.capabilities).mockResolvedValueOnce({ folderPicker: true })
    const user = userEvent.setup()
    render(<SettingsTab settings={settings} onSaved={() => {}} />)

    await user.click(await screen.findByRole('button', { name: 'Remove all data…' }))
    // confirm dialog appears; cancel does NOT wipe
    expect(screen.getByRole('alertdialog', { name: 'Remove all data?' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Remove all data' }))

    expect(api.wipeData).toHaveBeenCalledOnce()
    expect(await screen.findByText(/SyncSentinel is closing/)).toBeInTheDocument()
  })
})
