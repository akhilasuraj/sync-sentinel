using Microsoft.AspNetCore.SignalR;

namespace SyncSentinel.Core;

/// <summary>
/// Push channel to the UI. The server pushes status + log events to clients;
/// in Phase 0 it carries only the heartbeat <c>tick</c>.
/// </summary>
public sealed class StatusHub : Hub
{
}
