// Pure mapping from a path's existence to the editor's inline hint. Source and
// destination differ: a missing source is a problem, a missing destination is
// fine (robocopy creates it). The component maps `tone` to a colour.

export type PathRole = 'source' | 'destination'
export type PathTone = 'ok' | 'warn' | 'info'

export interface PathHint {
  tone: PathTone
  text: string
}

export function pathHint(role: PathRole, exists: boolean): PathHint {
  if (exists) return { tone: 'ok', text: 'Folder exists' }
  return role === 'source'
    ? { tone: 'warn', text: 'Folder not found' }
    : { tone: 'info', text: 'Will be created on first run' }
}
