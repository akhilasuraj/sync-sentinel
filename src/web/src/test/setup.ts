// Registers @testing-library/jest-dom matchers (toBeInTheDocument, etc.) and
// unmounts rendered components between tests (we run with globals:false, so
// RTL's automatic cleanup hook isn't installed — do it explicitly).
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'

afterEach(() => cleanup())
