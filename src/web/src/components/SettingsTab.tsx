import { useEffect, useState } from 'react'
import { api } from '../api'
import type { AppUpdateStatus, GlobalSettings } from '../types'
import FlagsEditor from './FlagsEditor'
import ConfirmDialog from './ConfirmDialog'

export default function SettingsTab({ settings, onSaved }: { settings: GlobalSettings; onSaved: () => void }) {
  const [form, setForm] = useState<GlobalSettings>(settings)
  const [saving, setSaving] = useState(false)
  const [inShell, setInShell] = useState(false)
  const [updateCapability, setUpdateCapability] = useState<'installed' | 'portable' | 'unavailable'>('unavailable')
  const [updateStatus, setUpdateStatus] = useState<AppUpdateStatus | null>(null)
  const [checkingUpdates, setCheckingUpdates] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [wiping, setWiping] = useState(false)
  const [wipeError, setWipeError] = useState(false)
  const set = <K extends keyof GlobalSettings>(key: K, value: GlobalSettings[K]) => setForm((f) => ({ ...f, [key]: value }))
  const dirty = JSON.stringify(form) !== JSON.stringify(settings)

  // The wipe-and-quit action only does anything in the desktop shell; hide it
  // elsewhere (same shell-presence signal the folder picker uses).
  useEffect(() => {
    api.capabilities().then((c) => {
      setInShell(c.folderPicker)
      setUpdateCapability(c.updates ?? 'unavailable')
      if (c.updates && c.updates !== 'unavailable') {
        api.getUpdateStatus().then(setUpdateStatus).catch(() => {})
      }
    }).catch(() => setInShell(false))
  }, [])

  async function save() {
    setSaving(true)
    try {
      await api.updateSettings(form)
      onSaved()
    } finally {
      setSaving(false)
    }
  }

  async function wipe() {
    setConfirming(false)
    setWiping(true)
    setWipeError(false)
    try {
      const res = await api.wipeData()
      if (!res.ok) {
        setWiping(false)
        setWipeError(true)
      }
      // On success the app removes its data and exits — this window closes shortly.
    } catch {
      // Network error / request aborted — surface it instead of hanging on "Removing…".
      setWiping(false)
      setWipeError(true)
    }
  }

  async function checkForUpdates() {
    setCheckingUpdates(true)
    try {
      setUpdateStatus(await api.checkForUpdates())
    } catch {
      setUpdateStatus({
        distribution: updateCapability === 'installed' ? 'installed' : 'portable',
        state: 'error', version: null, message: "Couldn't check for updates. Please try again.", releaseUrl: null,
      })
    } finally {
      setCheckingUpdates(false)
    }
  }

  return (
    <>
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

        <label className="mt-3 flex items-center gap-2">
          <input type="checkbox" checked={form.automaticUpdateChecks} onChange={(e) => set('automaticUpdateChecks', e.target.checked)} />
          <span className="text-sm">Check automatically for updates</span>
        </label>

        <div className="mt-6 flex items-center gap-2">
          <button className="btn" disabled={!dirty || saving} onClick={save}>{saving ? 'Saving…' : 'Save settings'}</button>
          {dirty && <button className="btn-ghost" disabled={saving} onClick={() => setForm(settings)}>Cancel</button>}
        </div>
      </section>

      {updateCapability !== 'unavailable' && (
        <section className="mt-6 max-w-xl rounded-2xl border border-edge bg-panel p-5">
          <h2 className="text-base font-semibold">Updates</h2>
          <p className="mt-1 text-sm text-slate-400">
            {updateCapability === 'installed'
              ? 'Installed copy — signed updates can be downloaded and installed in the app.'
              : 'Portable copy — update checks can open the release page, but never overwrite this executable.'}
          </p>
          {updateStatus && <p className="mt-3 text-sm text-slate-300">{updateStatus.message}</p>}
          {updateStatus?.releaseUrl && (
            <a className="mt-2 inline-block text-sm text-sentinel hover:underline" href={updateStatus.releaseUrl} target="_blank" rel="noreferrer">View release</a>
          )}
          <div className="mt-4">
            <button className="btn" disabled={checkingUpdates} onClick={checkForUpdates}>
              {checkingUpdates ? 'Checking…' : 'Check for updates'}
            </button>
          </div>
        </section>
      )}

      {inShell && (
        <section className="mt-6 max-w-xl rounded-2xl border border-red-500/30 bg-panel p-5">
          <h2 className="text-base font-semibold text-red-400">Danger zone</h2>
          <p className="mt-1 text-sm text-slate-400">
            Remove all SyncSentinel data — your jobs, settings, and run history — and the login-autostart entry, then close
            the app. Useful for the portable build, which has no uninstaller.
          </p>
          {wiping ? (
            <p className="mt-4 font-mono text-sm text-slate-300">Removing your data… SyncSentinel is closing.</p>
          ) : (
            <>
              {wipeError && <p className="mt-3 text-sm text-red-400">Couldn't remove the data. Please try again.</p>}
              <button
                className="mt-4 rounded-lg bg-red-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-red-600"
                onClick={() => setConfirming(true)}
              >
                Remove all data…
              </button>
            </>
          )}
        </section>
      )}

      {confirming && (
        <ConfirmDialog
          title="Remove all data?"
          message="This deletes every job, setting, and run-history entry, and removes the login-autostart entry — then SyncSentinel closes. It can't undo this, and it can't delete the program itself: remove SyncSentinel.exe and its SyncSentinel.exe.WebView2 folder manually to finish."
          confirmLabel="Remove all data"
          onConfirm={wipe}
          onCancel={() => setConfirming(false)}
        />
      )}
    </>
  )
}
