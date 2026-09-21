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

namespace SmartNotes.Core.Services;

/// <summary>
/// Service that checks GitHub Releases for new updates on application startup
/// and provides seamless 1-click automatic downloading, extraction, and in-place updating.
/// </summary>
public static class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/OlfJD/SmartNotes/releases/latest";
    public static readonly Version CurrentVersion = new(1, 1, 5);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        try
        {
            HttpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SmartNotes-AutoUpdater", CurrentVersion.ToString())
            );
        }
        catch { }
    }

    /// <summary>
    /// Checks for updates asynchronously against the GitHub repository.
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
                    MessageBox.Show(
                        "Unable to contact GitHub Releases right now. Please check your internet connection.",
                        "SmartNotes Updater",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
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

                    notifyCallback?.Invoke("SmartNotes Update Available", $"v{latestVer} is ready to install!");

                    var result = MessageBox.Show(
                        $"A new version of SmartNotes is available on GitHub!\n\n" +
                        $"• Installed Version: v{CurrentVersion}\n" +
                        $"• Latest Version: v{latestVer}\n\n" +
                        $"Would you like to download and apply this update now?",
                        "SmartNotes Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
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
                    MessageBox.Show(
                        $"SmartNotes is completely up to date! (v{CurrentVersion})",
                        "SmartNotes Updater",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
        }
        catch (Exception ex)
        {
            if (isManualCheck)
            {
                MessageBox.Show(
                    $"Update check failed: {ex.Message}",
                    "SmartNotes Updater",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
    }

    private static async Task DownloadAndApplyUpdateAsync(string downloadUrl)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SmartNotes_Update");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");
            string extractPath = Path.Combine(tempDir, "extracted");

            var zipBytes = await HttpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, zipBytes);

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            int currentPid = Environment.ProcessId;

            string scriptPath = Path.Combine(tempDir, "apply_update.bat");
            string scriptContent = $@"@echo off
timeout /t 1 /nobreak > nul
taskkill /F /PID {currentPid} > nul 2>&1
timeout /t 1 /nobreak > nul
xcopy /Y /E /I ""{extractPath}\*"" ""{appDir}\"" > nul
start """" ""{Path.Combine(appDir, "SmartNotes.exe")}""
exit
";
            await File.WriteAllTextAsync(scriptPath, scriptContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to auto-apply update: {ex.Message}\nOpening release page...",
                "SmartNotes Updater",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
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
