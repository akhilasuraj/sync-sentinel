import { useState } from 'react'

interface Props {
  items: string[]
  onAdd: (raw: string) => void
  onRemove: (item: string) => void
  placeholder?: string
}

/** Editable list of exclusion items shown as chips, each removable, with a
 *  tag-style input (Enter or comma commits; paste of many splits on add). */
export default function ChipList({ items, onAdd, onRemove, placeholder = 'add…' }: Props) {
  const [draft, setDraft] = useState('')
  const commit = () => {
    if (draft.trim()) onAdd(draft)
    setDraft('')
  }

  return (
    <div className="flex min-h-[40px] flex-wrap items-center gap-1.5 rounded-lg border border-edge bg-panel-2 p-2">
      {items.map((item) => (
        <span key={item} className="inline-flex items-center gap-1.5 rounded-md bg-white/5 py-0.5 pr-1 pl-2 font-mono text-xs text-slate-200">
          <span>{item}</span>
          <button
            type="button"
            aria-label={`Remove ${item}`}
            onClick={() => onRemove(item)}
            className="grid h-4 w-4 place-items-center rounded text-slate-500 transition hover:bg-red-500/15 hover:text-red-400"
          >
            ×
          </button>
        </span>
      ))}
      <input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ',') {
            e.preventDefault()
            commit()
          } else if (e.key === 'Backspace' && draft === '' && items.length > 0) {
            onRemove(items[items.length - 1])
          }
        }}
        onBlur={commit}
        placeholder={placeholder}
        className="min-w-[8ch] flex-1 bg-transparent px-1 py-0.5 font-mono text-xs text-slate-100 outline-none placeholder:text-slate-600"
      />
    </div>
  )
}
