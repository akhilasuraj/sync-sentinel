// Pure formatting helpers for run-history display — unit-tested without rendering.

export function statusDotClass(status: string): string {
  switch (status) {
    case 'Success':
      return 'bg-green-500'
    case 'Warning':
      return 'bg-amber-400'
    case 'Error':
      return 'bg-red-500'
    case 'Skipped':
      return 'bg-yellow-500'
    default:
      return 'bg-slate-500'
  }
}

export function runCounts(run: { filesCopied: number; filesExtra: number; filesFailed: number }): string {
  return `${run.filesCopied} copied · ${run.filesExtra} extra · ${run.filesFailed} failed`
}

export function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds.toFixed(1)}s`
  }
  const m = Math.floor(seconds / 60)
  const s = Math.round(seconds % 60)
  return `${m}m ${s}s`
}
