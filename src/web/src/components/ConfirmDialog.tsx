interface Props {
  title: string
  message: string
  confirmLabel?: string
  onConfirm: () => void
  onCancel: () => void
}

/** A small destructive-action confirmation. Used before deleting jobs and sets. */
export default function ConfirmDialog({ title, message, confirmLabel = 'Delete', onConfirm, onCancel }: Props) {
  return (
    <div className="fixed inset-0 z-20 grid place-items-center bg-black/60 p-4" onClick={onCancel}>
      <div
        role="alertdialog"
        aria-label={title}
        className="w-full max-w-sm rounded-2xl border border-edge bg-panel p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-base font-semibold text-slate-100">{title}</h2>
        <p className="mt-2 text-sm text-slate-400">{message}</p>
        <div className="mt-6 flex justify-end gap-2">
          <button className="btn-ghost" onClick={onCancel}>Cancel</button>
          <button
            className="rounded-lg bg-red-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-red-600"
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
