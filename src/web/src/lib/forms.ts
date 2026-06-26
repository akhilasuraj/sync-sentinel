// Pure helpers shared by the config forms — extracted so they can be unit-tested
// without rendering anything.

/** Split a free-text field into items on commas/whitespace, trimming + dropping blanks. */
export function splitItems(text: string): string[] {
  return text
    .split(/[\s,]+/)
    .map((s) => s.trim())
    .filter(Boolean)
}

/** Toggle an id in a selection list: add if absent, remove if present. */
export function toggleId(list: string[], id: string): string[] {
  return list.includes(id) ? list.filter((x) => x !== id) : [...list, id]
}
