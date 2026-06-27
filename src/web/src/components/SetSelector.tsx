import { toggleId } from '../lib/forms'

interface Props {
  label: string
  sets: { id: string; name: string }[]
  selectedIds: string[]
  onChange: (ids: string[]) => void
  emptyLabel: string
}

/** Attach exclusion sets to a job: every set is listed — attached ones show as
 *  chips with a red × to remove, the rest as "+ name" chips you click to add. */
export default function SetSelector({ label, sets, selectedIds, onChange, emptyLabel }: Props) {
  const selected = sets.filter((s) => selectedIds.includes(s.id))
  const available = sets.filter((s) => !selectedIds.includes(s.id))

  return (
    <div>
      <span className="mb-1 block text-sm text-slate-400">{label}</span>
      <div className="flex min-h-[44px] flex-wrap items-center gap-2 rounded-lg border border-edge bg-panel-2 p-2">
        {sets.length === 0 && <span className="px-1 text-xs text-slate-600">{emptyLabel}</span>}

        {selected.map((s) => (
          <span
            key={s.id}
            className="inline-flex items-center gap-1.5 rounded-full bg-sentinel/15 py-1 pr-1.5 pl-3 text-xs font-medium text-slate-100 ring-1 ring-sentinel/40"
          >
            {s.name}
            <button
              type="button"
              aria-label={`Remove ${s.name}`}
              onClick={() => onChange(toggleId(selectedIds, s.id))}
              className="grid h-4 w-4 place-items-center rounded-full text-slate-400 transition hover:bg-red-500/20 hover:text-red-400"
            >
              ×
            </button>
          </span>
        ))}

        {available.map((s) => (
          <button
            key={s.id}
            type="button"
            aria-label={`Add ${s.name}`}
            onClick={() => onChange(toggleId(selectedIds, s.id))}
            className="rounded-full border border-edge px-3 py-1 text-xs font-medium text-slate-400 transition hover:border-slate-500 hover:text-slate-200"
          >
            + {s.name}
          </button>
        ))}
      </div>
    </div>
  )
}
