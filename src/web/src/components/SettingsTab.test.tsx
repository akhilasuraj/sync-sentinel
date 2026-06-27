import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import SettingsTab from './SettingsTab'
import type { GlobalSettings } from '../types'

vi.mock('../api', () => ({ api: { updateSettings: vi.fn().mockResolvedValue(undefined) } }))

const settings: GlobalSettings = {
  defaultFlags: '/MIR',
  defaultIntervalMinutes: 15,
  maxConcurrent: 1,
  retention: { runsPerJob: 100, days: 30 },
  autostart: true,
}

describe('SettingsTab', () => {
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
})
