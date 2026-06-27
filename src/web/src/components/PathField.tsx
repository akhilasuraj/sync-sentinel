import { useEffect, useState } from 'react'
import { api } from '../api'
import { pathHint, type PathRole, type PathTone } from '../lib/pathStatus'

interface Props {
  label: string
  role: PathRole
  value: string
  onChange: (value: string) => void
  placeholder?: string
  pickerAvailable: boolean
}

const toneClass: Record<PathTone, string> = {
  ok: 'text-green-400/80',
  warn: 'text-amber-400',
  info: 'text-slate-500',
}

/**
 * A source/destination path input: editable text, an optional native-picker
 * Browse button (only when the shell provides one), and a quiet, debounced
 * existence hint underneath.
 */
export default function PathField({ label, role, value, onChange, placeholder, pickerAvailable }: Props) {
  const id = `path-${role}`
  const [exists, setExists] = useState<boolean | null>(null)

  // Debounced existence check behind the hint; clear stale state while typing.
  useEffect(() => {
    const path = value.trim()
    setExists(null)
    if (!path) return
    const t = setTimeout(() => {
      api.pathExists(path).then((r) => setExists(r.exists)).catch(() => setExists(null))
    }, 300)
    return () => clearTimeout(t)
  }, [value])

  async function browse() {
    const picked = await api.pickFolder({
      initialPath: value.trim() || undefined,
      title: `Select ${role} folder`,
    })
    if (picked) onChange(picked)
  }

  const hint = value.trim() && exists !== null ? pathHint(role, exists) : null

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-sm text-slate-400">{label}</label>
      <div className="relative">
        <input
          id={id}
          className={`field font-mono ${pickerAvailable ? 'pr-11' : ''}`}
          value={value}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
        {pickerAvailable && (
          <button
            type="button"
            onClick={browse}
            aria-label={`Browse for ${role} folder`}
            title="Browse…"
            className="absolute inset-y-1 right-1 grid w-9 place-items-center rounded-md text-slate-400 transition hover:bg-slate-700/50 hover:text-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
          >
            <FolderIcon />
          </button>
        )}
      </div>
      <p className={`mt-1 h-4 text-xs ${hint ? toneClass[hint.tone] : ''}`}>{hint?.text ?? ''}</p>
    </div>
  )
}

function FolderIcon() {
  return (
    <svg
      width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"
    >
      <path d="M4 5h5l2 2h9a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z" />
    </svg>
  )
}
