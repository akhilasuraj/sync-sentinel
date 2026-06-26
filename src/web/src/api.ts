import type {
  FileExclusionSet,
  FolderExclusionSet,
  GlobalSettings,
  Job,
  RunRecord,
  SyncSentinelConfig,
} from './types'

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return (await res.json()) as T
}

const post = (body: unknown): RequestInit => ({
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
})

const put = (body: unknown): RequestInit => ({
  method: 'PUT',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
})

export const api = {
  getConfig: () => fetch('/api/config').then(json<SyncSentinelConfig>),

  addJob: (j: Partial<Job>) => fetch('/api/jobs', post(j)).then(json<Job>),
  updateJob: (id: string, j: Job) => fetch(`/api/jobs/${id}`, put(j)),
  deleteJob: (id: string) => fetch(`/api/jobs/${id}`, { method: 'DELETE' }),
  runJob: (id: string) => fetch(`/api/jobs/${id}/run`, { method: 'POST' }),
  preview: (j: Partial<Job>) => fetch('/api/preview', post(j)).then(json<{ command: string }>),
  getRuns: (jobId: string) => fetch(`/api/jobs/${jobId}/runs`).then(json<RunRecord[]>),
  getRunLog: (runId: string) => fetch(`/api/runs/${runId}/log`).then((r) => (r.ok ? r.text() : Promise.reject(new Error(`${r.status}`)))),

  addFolderSet: (s: Partial<FolderExclusionSet>) => fetch('/api/folder-sets', post(s)).then(json<FolderExclusionSet>),
  updateFolderSet: (id: string, s: FolderExclusionSet) => fetch(`/api/folder-sets/${id}`, put(s)),
  deleteFolderSet: (id: string) => fetch(`/api/folder-sets/${id}`, { method: 'DELETE' }),

  addFileSet: (s: Partial<FileExclusionSet>) => fetch('/api/file-sets', post(s)).then(json<FileExclusionSet>),
  updateFileSet: (id: string, s: FileExclusionSet) => fetch(`/api/file-sets/${id}`, put(s)),
  deleteFileSet: (id: string) => fetch(`/api/file-sets/${id}`, { method: 'DELETE' }),

  updateSettings: (s: GlobalSettings) => fetch('/api/settings', put(s)),
}
