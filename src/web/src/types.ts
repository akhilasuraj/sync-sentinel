// Mirrors the SyncSentinel.Core domain model (serialized camelCase).

export interface FolderExclusionSet {
  id: string
  name: string
  folders: string[]
}

export interface FileExclusionSet {
  id: string
  name: string
  patterns: string[]
}

export interface Job {
  id: string
  name: string
  source: string
  destination: string
  folderSetIds: string[]
  fileSetIds: string[]
  flagsOverride: string | null
  intervalMinutes: number
  enabled: boolean
}

export interface RetentionSettings {
  runsPerJob: number
  days: number
}

export interface GlobalSettings {
  defaultFlags: string
  defaultIntervalMinutes: number
  maxConcurrent: number
  retention: RetentionSettings
  autostart: boolean
}

export interface SyncSentinelConfig {
  jobs: Job[]
  folderSets: FolderExclusionSet[]
  fileSets: FileExclusionSet[]
  settings: GlobalSettings
}

export interface RunRecord {
  id: string
  jobId: string
  jobName: string
  status: string
  startedUtc: string
  finishedUtc: string
  filesCopied: number
  filesSkipped: number
  filesFailed: number
  filesExtra: number
  exitCode: number
  logPath: string
  durationSeconds: number
}

export const blankJob = (): Job => ({
  id: '',
  name: '',
  source: '',
  destination: '',
  folderSetIds: [],
  fileSetIds: [],
  flagsOverride: null,
  intervalMinutes: 15,
  enabled: true,
})
