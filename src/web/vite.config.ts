import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Build straight into the .NET project's wwwroot so the shell serves the app
// as embedded static files.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../SyncSentinel/wwwroot',
    emptyOutDir: true,
  },
})
