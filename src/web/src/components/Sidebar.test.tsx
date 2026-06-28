import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import Sidebar from './Sidebar'

vi.mock('../api', () => ({ api: { getVersion: vi.fn().mockResolvedValue('0.5.0') } }))

describe('Sidebar', () => {
  it('shows the app version from the API, prefixed with v', async () => {
    render(<Sidebar route="dashboard" onNavigate={() => {}} connected />)
    expect(await screen.findByText('v0.5.0')).toBeInTheDocument()
  })

  it('renders the nav and connection status', () => {
    render(<Sidebar route="jobs" onNavigate={() => {}} connected={false} />)
    expect(screen.getByRole('button', { name: /Jobs/ })).toBeInTheDocument()
    expect(screen.getByText('offline')).toBeInTheDocument()
  })
})
