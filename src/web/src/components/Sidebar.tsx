import type { ReactNode } from 'react'

export type Route = 'dashboard' | 'jobs' | 'sets' | 'settings'

interface Props {
  route: Route
  onNavigate: (route: Route) => void
  connected: boolean
}

const NAV: { key: Route; label: string; icon: ReactNode }[] = [
  { key: 'dashboard', label: 'Dashboard', icon: <IconRadar /> },
  { key: 'jobs', label: 'Jobs', icon: <IconLayers /> },
  { key: 'sets', label: 'Exclusion Sets', icon: <IconFunnel /> },
  { key: 'settings', label: 'Settings', icon: <IconSliders /> },
]

/** The left rail: brand mark, primary navigation, and the live watch indicator. */
export default function Sidebar({ route, onNavigate, connected }: Props) {
  return (
    <aside className="flex w-56 shrink-0 flex-col border-r border-edge bg-panel-2 px-3 py-5">
      <div className="flex items-center gap-2.5 px-2">
        <Mark />
        <div className="leading-tight">
          <div className="text-[15px] font-semibold tracking-tight text-slate-100">SyncSentinel</div>
          <div className="eyebrow">Watch console</div>
        </div>
      </div>

      <nav className="mt-7 flex flex-col gap-1">
        {NAV.map((item) => {
          const active = route === item.key
          return (
            <button
              key={item.key}
              onClick={() => onNavigate(item.key)}
              aria-current={active ? 'page' : undefined}
              className={`nav-item relative ${active ? 'nav-item-active' : ''}`}
            >
              {active && <span className="absolute top-1.5 bottom-1.5 -left-3 w-0.5 rounded-full bg-sentinel" />}
              <span className={active ? 'text-sentinel' : 'text-slate-500'}>{item.icon}</span>
              {item.label}
            </button>
          )
        })}
      </nav>

      <div className="mt-auto flex items-center gap-2 px-2 pt-4 text-xs">
        <span className={`h-2 w-2 rounded-full ${connected ? 'animate-pulse bg-green-500' : 'bg-slate-600'}`} />
        <span className="font-mono tracking-wide text-slate-500">{connected ? 'watching' : 'offline'}</span>
      </div>
    </aside>
  )
}

// ── Marks & icons (inline SVG, currentColor, 1.6px stroke) ──────────────────

function Mark() {
  return (
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" aria-hidden className="text-sentinel">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.4" opacity="0.35" />
      <path d="M12 12 L12 4 A8 8 0 0 1 19 9 Z" fill="currentColor" opacity="0.85" />
      <circle cx="12" cy="12" r="2.1" fill="currentColor" />
    </svg>
  )
}

const svg = (children: ReactNode) => (
  <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
    {children}
  </svg>
)

function IconRadar() {
  return svg(
    <>
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="4.5" />
      <path d="M12 12 L18.5 7.5" />
    </>,
  )
}
function IconLayers() {
  return svg(
    <>
      <path d="M12 3 21 8 12 13 3 8 12 3Z" />
      <path d="M3 13 12 18 21 13" />
    </>,
  )
}
function IconFunnel() {
  return svg(<path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z" />)
}
function IconSliders() {
  return svg(
    <>
      <path d="M5 6h14M5 12h14M5 18h14" />
      <circle cx="9" cy="6" r="2" fill="var(--color-panel-2)" />
      <circle cx="15" cy="12" r="2" fill="var(--color-panel-2)" />
      <circle cx="8" cy="18" r="2" fill="var(--color-panel-2)" />
    </>,
  )
}
