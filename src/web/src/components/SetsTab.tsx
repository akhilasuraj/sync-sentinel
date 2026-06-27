import { useState } from 'react'
import { api } from '../api'
import { addChips } from '../lib/chips'
import type { FileExclusionSet, FolderExclusionSet } from '../types'
import ChipList from './ChipList'
import ConfirmDialog from './ConfirmDialog'

interface Props {
  folderSets: FolderExclusionSet[]
  fileSets: FileExclusionSet[]
  onChanged: () => void
}

export default function SetsTab({ folderSets, fileSets, onChanged }: Props) {
  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <SetGroup
        title="Folder exclusions"
        hint="Directory names robocopy skips (/XD)"
        itemLabel="folder"
        sets={folderSets.map((s) => ({ id: s.id, name: s.name, items: s.folders }))}
        onAdd={(name, items) => api.addFolderSet({ name, folders: items })}
        onUpdate={(id, name, items) => api.updateFolderSet(id, { id, name, folders: items })}
        onDelete={(id) => api.deleteFolderSet(id)}
        onChanged={onChanged}
      />
      <SetGroup
        title="File exclusions"
        hint="File patterns robocopy skips (/XF)"
        itemLabel="pattern"
        sets={fileSets.map((s) => ({ id: s.id, name: s.name, items: s.patterns }))}
        onAdd={(name, items) => api.addFileSet({ name, patterns: items })}
        onUpdate={(id, name, items) => api.updateFileSet(id, { id, name, patterns: items })}
        onDelete={(id) => api.deleteFileSet(id)}
        onChanged={onChanged}
      />
    </div>
  )
}

interface SetVM {
  id: string
  name: string
  items: string[]
}

interface GroupProps {
  title: string
  hint: string
  itemLabel: string
  sets: SetVM[]
  onAdd: (name: string, items: string[]) => Promise<unknown>
  onUpdate: (id: string, name: string, items: string[]) => Promise<unknown>
  onDelete: (id: string) => Promise<unknown>
  onChanged: () => void
}

function SetGroup({ title, hint, itemLabel, sets, onAdd, onUpdate, onDelete, onChanged }: GroupProps) {
  return (
    <section className="rounded-2xl border border-edge bg-panel p-5">
      <h2 className="text-base font-semibold text-slate-100">{title}</h2>
      <p className="mt-0.5 mb-4 text-sm text-slate-500">{hint}</p>

      <div className="space-y-3">
        {sets.map((s) => (
          <SetCard
            key={s.id}
            set={s}
            itemLabel={itemLabel}
            onSave={(name, items) => onUpdate(s.id, name, items).then(onChanged)}
            onDelete={() => onDelete(s.id).then(onChanged)}
          />
        ))}
        {sets.length === 0 && <p className="text-xs text-slate-600">None yet.</p>}
      </div>

      <NewSet itemLabel={itemLabel} onAdd={(name, items) => onAdd(name, items).then(onChanged)} />
    </section>
  )
}

const sameItems = (a: string[], b: string[]) => a.length === b.length && a.every((x, i) => x === b[i])

function SetCard({ set, itemLabel, onSave, onDelete }: { set: SetVM; itemLabel: string; onSave: (name: string, items: string[]) => void; onDelete: () => void }) {
  const [name, setName] = useState(set.name)
  const [items, setItems] = useState(set.items)
  const [confirming, setConfirming] = useState(false)
  const dirty = name.trim() !== set.name || !sameItems(items, set.items)

  return (
    <div className="rounded-xl border border-edge bg-panel-2 p-3">
      <div className="flex items-center gap-2">
        <input className="field flex-1" value={name} onChange={(e) => setName(e.target.value)} aria-label="Set name" />
        <button className="btn-ghost text-red-400" onClick={() => setConfirming(true)}>Delete</button>
      </div>
      <div className="mt-2">
        <ChipList
          items={items}
          placeholder={`add ${itemLabel}…`}
          onAdd={(raw) => setItems(addChips(items, raw))}
          onRemove={(it) => setItems(items.filter((x) => x !== it))}
        />
      </div>
      {dirty && (
        <div className="mt-2 flex justify-end gap-2">
          <button className="btn-ghost" onClick={() => { setName(set.name); setItems(set.items) }}>Cancel</button>
          <button className="btn" onClick={() => onSave(name.trim(), items)}>Save</button>
        </div>
      )}

      {confirming && (
        <ConfirmDialog
          title="Delete set?"
          message={`Delete “${set.name}”? Jobs that use it will lose these exclusions.`}
          onConfirm={() => { setConfirming(false); onDelete() }}
          onCancel={() => setConfirming(false)}
        />
      )}
    </div>
  )
}

function NewSet({ itemLabel, onAdd }: { itemLabel: string; onAdd: (name: string, items: string[]) => void }) {
  const [name, setName] = useState('')
  const [items, setItems] = useState<string[]>([])

  async function add() {
    if (!name.trim()) return
    await onAdd(name.trim(), items)
    setName('')
    setItems([])
  }

  return (
    <div className="mt-4 border-t border-edge pt-4">
      <div className="flex flex-col gap-2">
        <input className="field" placeholder="New set name" value={name} onChange={(e) => setName(e.target.value)} />
        <ChipList
          items={items}
          placeholder={`add ${itemLabel}…`}
          onAdd={(raw) => setItems(addChips(items, raw))}
          onRemove={(it) => setItems(items.filter((x) => x !== it))}
        />
        <button className="btn self-start" disabled={!name.trim()} onClick={add}>Add set</button>
      </div>
    </div>
  )
}
