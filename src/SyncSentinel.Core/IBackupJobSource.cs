namespace SyncSentinel.Core;

/// <summary>
/// Supplies the job to run. Phase 1 returns a single current/demo job; Phase 2
/// replaces this with the persisted job store.
/// </summary>
public interface IBackupJobSource
{
    BackupJob GetCurrent();
}
