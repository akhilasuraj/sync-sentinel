import { useState } from 'react'
import {
  FLAG_CATALOG,
  addFlag,
  lookupFlag,
  parseFlags,
  removeFlag,
  serializeFlags,
  updateValue,
  type FlagChip,
} from '../lib/robocopyFlags'

interface Props {
  value: string
  onChange: (next: string) => void
}

/** Edits a robocopy flags string as removable chips: pick from a described
 *  catalog, tweak value flags (/R:n) inline, or type a custom flag. */
export default function FlagsEditor({ value, onChange }: Props) {
  const chips = parseFlags(value)
  const [open, setOpen] = useState(false)
  const [custom, setCustom] = useState('')

  const emit = (next: FlagChip[]) => onChange(serializeFlags(next))
  const available = FLAG_CATALOG.filter((e) => !chips.some((c) => c.name.toLowerCase() === e.name.toLowerCase()))

  function addCustom() {
    if (!custom.trim()) return
    emit(addFlag(chips, custom.trim()))
    setCustom('')
  }

  return (
    <div>
      <div className="flex min-h-[42px] flex-wrap items-center gap-1.5 rounded-lg border border-edge bg-panel-2 p-2">
        {chips.map((c, i) => {
          const entry = lookupFlag(c.name)
          const hasValue = c.value !== null || entry?.takesValue
          return (
            <span
              key={`${c.name}-${i}`}
              title={entry?.description}
              className="inline-flex items-center gap-1 rounded-md bg-white/5 py-0.5 pr-1 pl-2 font-mono text-xs text-slate-200"
            >
              <span>{c.name}</span>
              {hasValue && (
                <>
                  <span className="text-slate-500">:</span>
                  <input
                    aria-label={`${c.name} value`}
                    value={c.value ?? ''}
                    inputMode={entry?.takesValue ? 'numeric' : undefined}
                    onChange={(e) => emit(updateValue(chips, i, e.target.value))}
                    className="w-10 rounded bg-black/30 px-1 text-center text-slate-100 outline-none focus:bg-black/50"
                  />
                </>
              )}
              <button
                type="button"
                aria-label={`Remove ${c.name}`}
                onClick={() => emit(removeFlag(chips, i))}
                className="grid h-4 w-4 place-items-center rounded text-slate-500 transition hover:bg-red-500/15 hover:text-red-400"
              >
                ×
              </button>
            </span>
          )
        })}
        {chips.length === 0 && <span className="px-1 text-xs text-slate-600">No flags — robocopy uses its defaults.</span>}
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <div className="relative">
          <button type="button" className="btn-ghost" disabled={available.length === 0} onClick={() => setOpen((o) => !o)}>
            + Add flag
          </button>
          {open && (
            <>
              <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
              <div className="absolute z-20 mt-1 max-h-72 w-80 overflow-auto rounded-lg border border-edge bg-panel shadow-xl">
                {available.map((e) => (
                  <button
                    key={e.name}
                    type="button"
                    onClick={() => { emit(addFlag(chips, e.takesValue ? `${e.name}:${e.defaultValue ?? ''}` : e.name)); setOpen(false) }}
                    className="block w-full border-b border-edge/60 px-3 py-2 text-left transition last:border-0 hover:bg-white/5"
                  >
                    <span className="font-mono text-xs text-sentinel">{e.name}{e.takesValue ? ':n' : ''}</span>
                    <span className="ml-2 text-xs font-medium text-slate-200">{e.label}</span>
                    <p className="mt-0.5 text-xs text-slate-500">{e.description}</p>
                  </button>
                ))}
              </div>
            </>
          )}
        </div>

        <input
          value={custom}
          onChange={(e) => setCustom(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addCustom() } }}
          placeholder="custom flag, e.g. /COPY:DAT"
          aria-label="Custom flag"
          className="min-w-[12ch] flex-1 rounded-lg border border-edge bg-panel-2 px-3 py-2 font-mono text-sm text-slate-100 outline-none focus:border-sentinel"
        />
        <button type="button" className="btn-ghost" disabled={!custom.trim()} onClick={addCustom}>Add</button>
      </div>
    </div>
  )
}
