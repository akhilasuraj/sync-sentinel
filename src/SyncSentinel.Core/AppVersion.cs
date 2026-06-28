namespace SyncSentinel.Core;

/// <summary>
/// The running app's display version, surfaced by <c>GET /api/version</c> for the
/// UI. Provided via DI so the shell can supply the real value (stamped into its
/// assembly at publish time); the shared wiring registers a dev default, and tests
/// can inject a known value.
/// </summary>
public sealed record AppVersion(string Value)
{
    public const string Dev = "0.0.0-dev";
}
