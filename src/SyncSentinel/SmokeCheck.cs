using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

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

        try
        {
            var cfg = await http.GetStringAsync("/api/config");
            var configOk = cfg.Contains("DeveloperDefaults");
            ok &= configOk;
            Report($"[config] {(configOk ? "PASS" : "FAIL")}  seeded ({cfg.Length} bytes)");
        }
        catch (Exception ex)
        {
            ok = false;
            Report($"[config] FAIL  {ex.Message}");
        }

        try
        {
            // Create a real job (source under %TEMP%, excluding bin via the seeded
            // DeveloperDefaults set), run it by id, and confirm the mirror.
            var root = Path.Combine(Path.GetTempPath(), "SyncSentinelSmokeRun");
            var src = Path.Combine(root, "src");
            var dst = Path.Combine(root, "dst");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(Path.Combine(src, "bin"));
            File.WriteAllText(Path.Combine(src, "readme.txt"), "smoke");
            File.WriteAllText(Path.Combine(src, "bin", "ignored.dll"), "x");

            var create = await http.PostAsJsonAsync("/api/jobs", new
            {
                name = "Smoke",
                source = src,
                destination = dst,
                folderSetIds = new[] { "developer-defaults" },
            });
            using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var jobId = doc.RootElement.GetProperty("id").GetString();

            var post = await http.PostAsync($"/api/jobs/{jobId}/run", null);
            var mirrored = false;
            for (var i = 0; i < 50 && !mirrored; i++)
            {
                if (File.Exists(Path.Combine(dst, "readme.txt"))) mirrored = true;
                else await Task.Delay(200);
            }
            var binExcluded = !Directory.Exists(Path.Combine(dst, "bin"));
            var runOk = create.IsSuccessStatusCode && post.IsSuccessStatusCode && mirrored && binExcluded;
            ok &= runOk;
            Report($"[run]    {(runOk ? "PASS" : "FAIL")}  job={jobId} posted={post.StatusCode} mirrored={mirrored} binExcluded={binExcluded}");
        }
        catch (Exception ex)
        {
            ok = false;
            Report($"[run]    FAIL  {ex.Message}");
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
