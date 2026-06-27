// The robocopy behaviour-flags model for the flag editor. Flags are persisted as
// one space-separated string (e.g. "/MIR /XJ /R:3 /W:5"); here we parse that into
// chips, edit them, and serialize back. A small client-side catalog gives each
// flag a human description and marks the ones that take a value (/R:n, /W:n, /MT:n).

export interface FlagChip {
  name: string // e.g. "/MIR" or "/R"
  value: string | null // e.g. "3" for /R:3, null for a toggle flag
}

export interface CatalogEntry {
  name: string
  label: string
  description: string
  takesValue: boolean
  defaultValue?: string
}

export const FLAG_CATALOG: CatalogEntry[] = [
  { name: '/MIR', label: 'Mirror', description: 'Mirror the source: copy it and delete extras in the destination to match.', takesValue: false },
  { name: '/E', label: 'Subdirectories', description: 'Copy subdirectories, including empty ones.', takesValue: false },
  { name: '/XJ', label: 'Exclude junctions', description: 'Skip junction points and symlinks (avoids loops).', takesValue: false },
  { name: '/FFT', label: 'FAT file times', description: 'Assume 2-second time granularity — helps with OneDrive / NAS.', takesValue: false },
  { name: '/Z', label: 'Restartable', description: 'Restartable mode: resume a file copy that was interrupted.', takesValue: false },
  { name: '/COPYALL', label: 'Copy all info', description: 'Copy all file info: data, attributes, timestamps, ACLs, owner, auditing.', takesValue: false },
  { name: '/NP', label: 'No progress', description: "Don't show the per-file percentage copied.", takesValue: false },
  { name: '/NFL', label: 'No file list', description: "Don't log file names.", takesValue: false },
  { name: '/NDL', label: 'No dir list', description: "Don't log directory names.", takesValue: false },
  { name: '/R', label: 'Retries', description: 'Number of retries on a failed copy.', takesValue: true, defaultValue: '3' },
  { name: '/W', label: 'Wait', description: 'Seconds to wait between retries.', takesValue: true, defaultValue: '5' },
  { name: '/MT', label: 'Multi-threaded', description: 'Copy with N threads (1–128).', takesValue: true, defaultValue: '8' },
]

export function parseFlags(flags: string): FlagChip[] {
  return flags
    .split(/\s+/)
    .map((t) => t.trim())
    .filter(Boolean)
    .map((tok) => {
      const i = tok.indexOf(':')
      return i === -1 ? { name: tok, value: null } : { name: tok.slice(0, i), value: tok.slice(i + 1) }
    })
}

export function serializeFlags(chips: FlagChip[]): string {
  return chips.map((c) => (c.value === null || c.value === '' ? c.name : `${c.name}:${c.value}`)).join(' ')
}

export function lookupFlag(name: string): CatalogEntry | undefined {
  return FLAG_CATALOG.find((e) => e.name.toLowerCase() === name.toLowerCase())
}

export function addFlag(chips: FlagChip[], raw: string): FlagChip[] {
  const [parsed] = parseFlags(raw)
  if (!parsed) return chips
  if (chips.some((c) => c.name.toLowerCase() === parsed.name.toLowerCase())) return chips
  return [...chips, parsed]
}

export function updateValue(chips: FlagChip[], index: number, value: string): FlagChip[] {
  return chips.map((c, i) => (i === index ? { ...c, value } : c))
}

export function removeFlag(chips: FlagChip[], index: number): FlagChip[] {
  return chips.filter((_, i) => i !== index)
}
