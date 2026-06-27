// Adding exclusion items as chips: parse a free-text entry (comma/space/paste)
// into items and append the new ones, de-duplicating case-insensitively (robocopy
// /XD and /XF are case-insensitive on Windows) while keeping the first casing.

import { splitItems } from './forms'

export function addChips(existing: string[], raw: string): string[] {
  const seen = new Set(existing.map((s) => s.toLowerCase()))
  const result = [...existing]
  for (const item of splitItems(raw)) {
    const key = item.toLowerCase()
    if (!seen.has(key)) {
      seen.add(key)
      result.push(item)
    }
  }
  return result
}
