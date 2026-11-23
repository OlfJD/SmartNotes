using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using SmartNotes.UI.Windows;

namespace SmartNotes.Core.Services;

/// <summary>
/// Service that checks GitHub Releases for new updates on application startup
/// and provides seamless 1-click automatic downloading, extraction, and in-place updating.
/// </summary>
public static class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/OlfJD/SmartNotes/releases/latest";
    // Read from the assembly (csproj <Version>) so it can't drift from the shipped build; a stale
    // hard-coded value would keep offering the same "update" forever.
    public static readonly Version CurrentVersion = GetAssemblyVersion();

    private static Version GetAssemblyVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // The release zip (~2 MB) shared the 15 s limit above, so on a connection slower than ~1 Mbit/s every
    // download timed out and the app never updated. Downloads get their own, generous limit.
    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    static UpdateService()
    {
        try
        {
            var agent = new ProductInfoHeaderValue("SmartNotes-AutoUpdater", CurrentVersion.ToString());
            HttpClient.DefaultRequestHeaders.UserAgent.Add(agent);
            DownloadClient.DefaultRequestHeaders.UserAgent.Add(agent);
        }
        catch { }
    }

    // ---------------------------------------------------------------------------------------------
    // Silent automatic updates (no dialogs, no toasts)
    //   * check GitHub shortly after start and every few hours;
    //   * download + extract a newer release into a staging folder in the background;
    //   * restart into it only when the caller says it is a good moment (user idle, no note focused).
    // The manual "Check for Updates…" menu item below still shows its dialogs: the user asked for it.
    // ---------------------------------------------------------------------------------------------
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan ApplyPollInterval = TimeSpan.FromMinutes(1);

    private static System.Windows.Threading.DispatcherTimer? _checkTimer;
    private static System.Windows.Threading.DispatcherTimer? _applyTimer;
    private static string? _stagedDir;      // extracted, ready-to-copy release
    private static Version? _stagedVersion;
    private static bool _busy;
    private static bool _applying;

    /// <param name="isGoodMomentToRestart">Called once a minute while an update is staged; return true to restart into it now.</param>
    public static void StartAutomaticUpdates(Func<bool> isGoodMomentToRestart)
    {
        if (_checkTimer != null) return;
        _checkTimer = new System.Windows.Threading.DispatcherTimer { Interval = FirstCheckDelay };
        _checkTimer.Tick += async (_, _) =>
        {
            _checkTimer.Interval = CheckInterval;
            await CheckAndStageSilentlyAsync();
        };
        _checkTimer.Start();

        _applyTimer = new System.Windows.Threading.DispatcherTimer { Interval = ApplyPollInterval };
        _applyTimer.Tick += (_, _) =>
        {
            if (_stagedDir == null || _applying) return;
            bool now;
            try { now = isGoodMomentToRestart(); } catch { now = false; }
            if (now) ApplyStaged();
        };
        _applyTimer.Start();
    }

    private static async Task CheckAndStageSilentlyAsync()
    {
        if (_busy || _stagedDir != null || _applying) return;
        _busy = true;
        try
        {
            var latest = await GetLatestReleaseAsync();
            if (latest == null || latest.Value.version <= CurrentVersion || string.IsNullOrEmpty(latest.Value.zipUrl)) return;

            string dir = Path.Combine(Path.GetTempPath(), "SmartNotes_Update");
            string zipPath = Path.Combine(dir, "update.zip");
            string extractPath = Path.Combine(dir, "extracted");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var bytes = await DownloadClient.GetByteArrayAsync(latest.Value.zipUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);
            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            // Only stage a package that actually contains the app
            if (!File.Exists(Path.Combine(extractPath, "SmartNotes.exe")))
            {
                AppLog.Write($"[Update] v{latest.Value.version} downloaded, but the zip has no SmartNotes.exe; not installing it.");
                return;
            }
            _stagedDir = extractPath;
            _stagedVersion = latest.Value.version;
            AppLog.Write($"[Update] v{_stagedVersion} downloaded; restarting into it once the user is away.");
        }
        catch (Exception ex)
        {
            // offline, rate-limited, disk full …: try again at the next interval, without bothering the user
            AppLog.Error("[Update] Background update check", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task<(Version version, string? zipUrl)?> GetLatestReleaseAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");
        using var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        string clean = tag.Trim().TrimStart('v', 'V');
        if (!(Version.TryParse(clean, out var ver) || TryParseLooseVersion(clean, out ver))) return null;
        return (ver, FindZipAsset(root));
    }

    private static string? FindZipAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return url;
        }
        return null;
    }

    /// <summary>Restarts into the staged release: the app exits normally (pending note saves are flushed in OnExit),
    /// a hidden script copies the new files over the app folder and starts it again.</summary>
    private static void ApplyStaged()
    {
        if (_stagedDir == null || _applying) return;
        _applying = true;
        try
        {
            StartCopyScript(_stagedDir, restart: true);
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            AppLog.Error("[Update] Restarting into the downloaded version", ex);
            _applying = false; // stay on this version; the next check stages again
            _stagedDir = null;
        }
    }

    /// <summary>Writes and starts (hidden) the script that waits for this process to exit, copies the new
    /// files over the app folder and, if asked, starts the app again.</summary>
    private static void StartCopyScript(string extractPath, bool restart)
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        int pid = Environment.ProcessId;
        string scriptPath = Path.Combine(Path.GetDirectoryName(extractPath)!, "apply_update.bat");
        // Wait (up to ~30 s) for the app to exit on its own so pending note saves are flushed;
        // only force-kill as a last resort.
        string script = $@"@echo off
set /a tries=0
:waitloop
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if errorlevel 1 goto apply
set /a tries+=1
if %tries% geq 30 (
    taskkill /F /PID {pid} > nul 2>&1
    timeout /t 1 /nobreak > nul
    goto apply
)
timeout /t 1 /nobreak > nul
goto waitloop
:apply
xcopy /Y /E /I ""{extractPath}\*"" ""{appDir}\"" > nul
{(restart ? $@"start """" ""{Path.Combine(appDir, "SmartNotes.exe")}""" : "")}
exit
";
        File.WriteAllText(scriptPath, script);
        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    /// <summary>
    /// Manual check from the "Check for Updates…" menu item: reports the result in a dialog.
    /// </summary>
    public static async Task CheckForUpdatesAsync(bool isManualCheck = false, Action<string, string>? notifyCallback = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                if (isManualCheck)
                {
                    ModernMessageBox.Show(
                        LocalizationService.T("Update_ErrorNetwork"),
                        LocalizationService.T("Update_DialogTitle"),
                        ModernMessageButtons.OK,
                        ModernMessageIcon.Warning,
                        LocalizationService.T("Update_ConnectionNotice")
                    );
                }
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "https://github.com/OlfJD/SmartNotes/releases/latest";
            string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            string cleanTag = tagName.Trim().TrimStart('v', 'V');
            if (Version.TryParse(cleanTag, out var latestVer) || TryParseLooseVersion(cleanTag, out latestVer))
            {
                if (latestVer > CurrentVersion)
                {
                    // Find release zip asset
                    string? downloadUrl = null;
                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = url;
                                break;
                            }
                        }
                    }

                    notifyCallback?.Invoke(
                        LocalizationService.T("Update_ToastTitle"),
                        LocalizationService.T("Update_ToastMsg", latestVer)
                    );

                    var result = ModernMessageBox.Show(
                        LocalizationService.T("Update_AvailableMsg", CurrentVersion, latestVer),
                        LocalizationService.T("Update_DialogTitle"),
                        ModernMessageButtons.YesNo,
                        ModernMessageIcon.Update,
                        LocalizationService.T("Update_AvailableTitle")
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            await DownloadAndApplyUpdateAsync(downloadUrl);
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = htmlUrl,
                                UseShellExecute = true
                            });
                        }
                    }
                }
                else if (isManualCheck)
                {
                    ModernMessageBox.Show(
                        LocalizationService.T("Update_UpToDateMsg", CurrentVersion),
                        LocalizationService.T("Update_DialogTitle"),
                        ModernMessageButtons.OK,
                        ModernMessageIcon.Success,
                        LocalizationService.T("Update_UpToDateTitle")
                    );
                }
            }
        }
        catch (Exception ex)
        {
            if (isManualCheck)
            {
                ModernMessageBox.Show(
                    LocalizationService.T("Update_ErrorCheck", ex.Message),
                    LocalizationService.T("Update_DialogTitle"),
                    ModernMessageButtons.OK,
                    ModernMessageIcon.Warning,
                    LocalizationService.T("Update_ConnectionNotice")
                );
            }
        }
    }

    private static async Task DownloadAndApplyUpdateAsync(string downloadUrl)
    {
        if (_busy || _applying) return; // a silent download / restart is already under way
        try
        {
            _stagedDir = null; // the folder is about to be replaced
            string tempDir = Path.Combine(Path.GetTempPath(), "SmartNotes_Update");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");
            string extractPath = Path.Combine(tempDir, "extracted");

            var zipBytes = await DownloadClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, zipBytes);

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            _applying = true;
            StartCopyScript(extractPath, restart: true);
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            ModernMessageBox.Show(
                LocalizationService.T("Update_ErrorApply", ex.Message),
                LocalizationService.T("Update_DialogTitle"),
                ModernMessageButtons.OK,
                ModernMessageIcon.Warning,
                LocalizationService.T("Update_ManualRequired")
            );
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/OlfJD/SmartNotes/releases/latest",
                UseShellExecute = true
            });
        }
    }

    private static bool TryParseLooseVersion(string text, out Version version)
    {
        version = new Version(1, 0, 0);
        var parts = text.Split('.');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
        {
            int build = (parts.Length >= 3 && int.TryParse(parts[2], out int b)) ? b : 0;
            version = new Version(major, minor, build);
            return true;
        }
        return false;
    }
}
