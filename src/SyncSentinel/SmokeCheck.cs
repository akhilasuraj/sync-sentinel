using System.Runtime.InteropServices;
using System.Text;

namespace SyncSentinel;

/// <summary>
/// Headless integration check for the REAL hosting path (Kestrel on a loopback
/// socket + static-file serving of the React build) — the bits the in-memory
/// xUnit TestServer doesn't cover. Run via `SyncSentinel.exe --smoke`; writes
/// results to the console and to %TEMP%\syncsentinel_smoke.log, and sets the
/// process exit code (0 = pass).
/// </summary>
internal static class SmokeCheck
{
    public static async Task<bool> Run(string baseUrl)
    {
        AttachConsole(-1); // attach to the launching console so writes are visible
        var log = new StringBuilder();
        var ok = true;

        void Report(string line)
        {
            Console.WriteLine(line);
            log.AppendLine(line);
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

        try
        {
            var ping = await http.GetStringAsync("/api/ping");
            var pingOk = ping.Contains("pong");
            ok &= pingOk;
            Report($"[ping]   {(pingOk ? "PASS" : "FAIL")}  {ping}");
        }
        catch (Exception ex)
        {
            ok = false;
            Report($"[ping]   FAIL  {ex.Message}");
        }

        try
        {
            var html = await http.GetStringAsync("/");
            var staticOk = html.Contains("id=\"root\"");
            ok &= staticOk;
            Report($"[static] {(staticOk ? "PASS" : "FAIL")}  index.html served ({html.Length} bytes)");
        }
        catch (Exception ex)
        {
            ok = false;
            Report($"[static] FAIL  {ex.Message}");
        }

        Report($"[smoke]  {(ok ? "PASS" : "FAIL")}  {baseUrl}");

        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "syncsentinel_smoke.log"),
            log.ToString());
        return ok;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
}
