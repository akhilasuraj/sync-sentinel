import { useEffect, useState } from 'react'
import { api } from '../api'
import type { FileExclusionSet, FolderExclusionSet, Job } from '../types'
import PathField from './PathField'
import FlagsEditor from './FlagsEditor'
import SetSelector from './SetSelector'

interface Props {
  job: Job
  folderSets: FolderExclusionSet[]
  fileSets: FileExclusionSet[]
  onSaved: () => void
  onCancel: () => void
}

export default function JobEditor({ job, folderSets, fileSets, onSaved, onCancel }: Props) {
  const [form, setForm] = useState<Job>(job)
  const [preview, setPreview] = useState('')
  const [saving, setSaving] = useState(false)
  const [pickerAvailable, setPickerAvailable] = useState(false)
  const set = <K extends keyof Job>(key: K, value: Job[K]) => setForm((f) => ({ ...f, [key]: value }))

  // The native Browse picker exists only in the desktop shell; hide it otherwise.
  useEffect(() => {
    api.capabilities().then((c) => setPickerAvailable(c.folderPicker)).catch(() => setPickerAvailable(false))
  }, [])

  // Live "effective command" preview — reflects exactly what robocopy will run.
  useEffect(() => {
    const t = setTimeout(() => {
      api.preview(form).then((r) => setPreview(r.command)).catch(() => setPreview('(preview unavailable)'))
    }, 200)
    return () => clearTimeout(t)
  }, [form])

  async function save() {
    setSaving(true)
    try {
      if (form.id) await api.updateJob(form.id, form)
      else await api.addJob(form)
      onSaved()
    } finally {
      setSaving(false)
    }
  }

  const canSave = form.name.trim() && form.source.trim() && form.destination.trim()

  return (
    <div className="fixed inset-0 z-10 grid place-items-center bg-black/50 p-4" onClick={onCancel}>
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-auto rounded-2xl border border-edge bg-panel p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mb-4 text-lg font-semibold">{form.id ? 'Edit job' : 'New job'}</h2>

        <label className="mb-3 block">
          <span className="mb-1 block text-sm text-slate-400">Name</span>
          <input className="field" value={form.name} onChange={(e) => set('name', e.target.value)} />
        </label>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <PathField
            label="Source" role="source" value={form.source}
            onChange={(v) => set('source', v)} placeholder="C:\dev\MyProject"
            pickerAvailable={pickerAvailable}
          />
          <PathField
            label="Destination" role="destination" value={form.destination}
            onChange={(v) => set('destination', v)} placeholder="D:\Backup\MyProject"
            pickerAvailable={pickerAvailable}
          />
        </div>

        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <SetSelector
            label="Folder exclusion sets"
            sets={folderSets}
            selectedIds={form.folderSetIds}
            onChange={(ids) => set('folderSetIds', ids)}
            emptyLabel="No folder sets yet — create them in Exclusion Sets."
          />
          <SetSelector
            label="File exclusion sets"
            sets={fileSets}
            selectedIds={form.fileSetIds}
            onChange={(ids) => set('fileSetIds', ids)}
            emptyLabel="No file sets yet — create them in Exclusion Sets."
          />
        </div>

        <div className="mt-4">
          <label className="block max-w-[12rem]">
            <span className="mb-1 block text-sm text-slate-400">Interval (minutes)</span>
            <input type="number" min={1} className="field" value={form.intervalMinutes} onChange={(e) => set('intervalMinutes', Number(e.target.value))} />
          </label>
        </div>

        <div className="mt-4">
          <span className="mb-1 block text-sm text-slate-400">Flags override <span className="text-slate-600">(leave empty to use the global default)</span></span>
          <FlagsEditor value={form.flagsOverride ?? ''} onChange={(v) => set('flagsOverride', v || null)} />
        </div>

        <label className="mt-4 flex items-center gap-2">
          <input type="checkbox" checked={form.enabled} onChange={(e) => set('enabled', e.target.checked)} />
          <span className="text-sm">Enabled (scheduled)</span>
        </label>

        <div className="mt-4">
          <span className="mb-1 block text-sm text-slate-400">Effective command</span>
          <pre className="overflow-x-auto rounded-lg border border-edge bg-panel-2 p-3 font-mono text-xs leading-relaxed text-slate-300">{preview || '…'}</pre>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button className="btn-ghost" onClick={onCancel}>Cancel</button>
          <button className="btn" disabled={!canSave || saving} onClick={save}>{saving ? 'Saving…' : 'Save'}</button>
        </div>
      </div>
    </div>
  )
}
