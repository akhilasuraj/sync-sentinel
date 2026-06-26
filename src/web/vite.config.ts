import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Build straight into the .NET project's wwwroot so the shell serves the app
// as embedded static files.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../SyncSentinel/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      onwarn(warning, warn) {
        // @microsoft/signalr ships /*#__PURE__*/ comments in positions Rolldown
        // can't use — harmless dependency noise, not our code.
        if (warning.code === 'INVALID_ANNOTATION' || warning.message?.includes('__PURE__')) return
        warn(warning)
      },
    },
  },
})
