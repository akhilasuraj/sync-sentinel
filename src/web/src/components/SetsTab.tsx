import { useState } from 'react'
import { api } from '../api'
import { splitItems } from '../lib/forms'
import type { FileExclusionSet, FolderExclusionSet } from '../types'

interface Props {
  folderSets: FolderExclusionSet[]
  fileSets: FileExclusionSet[]
  onChanged: () => void
}

export default function SetsTab({ folderSets, fileSets, onChanged }: Props) {
  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <SetGroup
        title="Folder exclusion sets"
        hint="Directory names to exclude (robocopy /XD)"
        sets={folderSets.map((s) => ({ id: s.id, name: s.name, items: s.folders }))}
        onAdd={(name, items) => api.addFolderSet({ name, folders: items })}
        onUpdate={(id, name, items) => api.updateFolderSet(id, { id, name, folders: items })}
        onDelete={(id) => api.deleteFolderSet(id)}
        onChanged={onChanged}
      />
      <SetGroup
        title="File exclusion sets"
        hint="File patterns to exclude (robocopy /XF)"
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
  sets: SetVM[]
  onAdd: (name: string, items: string[]) => Promise<unknown>
  onUpdate: (id: string, name: string, items: string[]) => Promise<unknown>
  onDelete: (id: string) => Promise<unknown>
  onChanged: () => void
}

function SetGroup({ title, hint, sets, onAdd, onUpdate, onDelete, onChanged }: GroupProps) {
  const [newName, setNewName] = useState('')
  const [newItems, setNewItems] = useState('')

  async function add() {
    if (!newName.trim()) return
    await onAdd(newName.trim(), splitItems(newItems))
    setNewName('')
    setNewItems('')
    onChanged()
  }

  return (
    <section className="rounded-2xl border border-edge bg-panel p-5">
      <h2 className="text-base font-semibold">{title}</h2>
      <p className="mb-4 text-sm text-slate-400">{hint}</p>

      <div className="space-y-3">
        {sets.map((s) => (
          <SetRow key={s.id} set={s} onSave={(name, items) => onUpdate(s.id, name, items).then(onChanged)} onDelete={() => onDelete(s.id).then(onChanged)} />
        ))}
        {sets.length === 0 && <p className="text-xs text-slate-600">None yet.</p>}
      </div>

      <div className="mt-4 border-t border-edge pt-4">
        <div className="flex flex-col gap-2">
          <input className="field" placeholder="New set name" value={newName} onChange={(e) => setNewName(e.target.value)} />
          <input className="field font-mono" placeholder="items, comma or space separated" value={newItems} onChange={(e) => setNewItems(e.target.value)} />
          <button className="btn self-start" disabled={!newName.trim()} onClick={add}>Add set</button>
        </div>
      </div>
    </section>
  )
}

function SetRow({ set, onSave, onDelete }: { set: SetVM; onSave: (name: string, items: string[]) => void; onDelete: () => void }) {
  const [name, setName] = useState(set.name)
  const [items, setItems] = useState(set.items.join(', '))
  const dirty = name !== set.name || items !== set.items.join(', ')

  return (
    <div className="rounded-lg border border-edge bg-panel-2 p-3">
      <div className="flex items-center gap-2">
        <input className="field flex-1" value={name} onChange={(e) => setName(e.target.value)} />
        <button className="btn-ghost text-red-400" onClick={onDelete} title="Delete set">✕</button>
      </div>
      <input className="field mt-2 font-mono text-xs" value={items} onChange={(e) => setItems(e.target.value)} />
      {dirty && (
        <button className="btn mt-2" onClick={() => onSave(name.trim(), splitItems(items))}>Save changes</button>
      )}
    </div>
  )
}
