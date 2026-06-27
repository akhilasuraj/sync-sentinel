import { useState } from 'react'
import { api } from '../api'
import type { GlobalSettings } from '../types'
import FlagsEditor from './FlagsEditor'

export default function SettingsTab({ settings, onSaved }: { settings: GlobalSettings; onSaved: () => void }) {
  const [form, setForm] = useState<GlobalSettings>(settings)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof GlobalSettings>(key: K, value: GlobalSettings[K]) => setForm((f) => ({ ...f, [key]: value }))

  async function save() {
    setSaving(true)
    try {
      await api.updateSettings(form)
      onSaved()
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="max-w-xl rounded-2xl border border-edge bg-panel p-5">
      <h2 className="mb-4 text-base font-semibold">Settings</h2>

      <div className="mb-4">
        <span className="mb-1 block text-sm text-slate-400">Default robocopy flags</span>
        <FlagsEditor value={form.defaultFlags} onChange={(v) => set('defaultFlags', v)} />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">Default interval (min)</span>
          <input type="number" min={1} className="field" value={form.defaultIntervalMinutes} onChange={(e) => set('defaultIntervalMinutes', Number(e.target.value))} />
        </label>
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">Max concurrent runs</span>
          <input type="number" min={1} className="field" value={form.maxConcurrent} onChange={(e) => set('maxConcurrent', Number(e.target.value))} />
        </label>
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">Retention: runs per job</span>
          <input type="number" min={1} className="field" value={form.retention.runsPerJob} onChange={(e) => set('retention', { ...form.retention, runsPerJob: Number(e.target.value) })} />
        </label>
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">Retention: days</span>
          <input type="number" min={1} className="field" value={form.retention.days} onChange={(e) => set('retention', { ...form.retention, days: Number(e.target.value) })} />
        </label>
      </div>

      <label className="mt-4 flex items-center gap-2">
        <input type="checkbox" checked={form.autostart} onChange={(e) => set('autostart', e.target.checked)} />
        <span className="text-sm">Start automatically on login</span>
      </label>

      <div className="mt-6">
        <button className="btn" disabled={saving} onClick={save}>{saving ? 'Saving…' : 'Save settings'}</button>
      </div>
    </section>
  )
}
