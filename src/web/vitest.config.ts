import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Test-only config (kept separate from vite.config.ts so the production build
// stays decoupled from the test toolchain). jsdom for component tests; the
// pure-logic tests need no DOM but run fine here too.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
  },
})
