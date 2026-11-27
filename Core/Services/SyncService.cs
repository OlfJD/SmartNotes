using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using SmartNotes.Core.Models;

namespace SmartNotes.Core.Services;

/// <summary>
/// Keeps the notes the same on several PCs through a folder the user already syncs (OneDrive, Dropbox, a NAS).
/// No account and no server: the notes stay in the local data folder as before, and each PC also writes a copy of
/// them to &lt;sync folder&gt;\SmartNotes Sync\notes-&lt;pc&gt;-&lt;id&gt;.json, 3 s after its last save. It reads the other PCs'
/// files when they change (file watcher, plus a check every minute because watchers miss events on network and
/// cloud folders) and merges them with <see cref="SyncMerge"/>. Every PC only ever writes its own file.
/// Before the first sync with a folder, the notes are backed up (backups\&lt;date&gt;-before-sync).
/// </summary>
public sealed class SyncService : IDisposable
{
    public const string SubfolderName = "SmartNotes Sync";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static string TombstoneFile => Path.Combine(AppPaths.DataFolder, "sync-deleted.json");

    private readonly NoteStorageService _storage;
    private readonly SettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _writeTimer;
    private readonly DispatcherTimer _readTimer;
    private readonly DispatcherTimer _pollTimer;
    private readonly Dictionary<Guid, DateTime> _deleted;
    private FileSystemWatcher? _watcher;
    private string _lastWritten = "";
    private readonly Dictionary<string, DateTime> _seenFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Changes from the other PCs, to be applied to the open notes (on the UI thread).</summary>
    public event Action<SyncPlan>? PlanReady;

    /// <summary>For Settings: when this PC last read the folder and how many other PCs it saw there.</summary>
    public DateTime? LastSync { get; private set; }
    public int OtherDevices { get; private set; }
    public string? LastError { get; private set; }

    public SyncService(NoteStorageService storage, SettingsService settings, Dispatcher dispatcher)
    {
        _storage = storage;
        _settings = settings;
        _dispatcher = dispatcher;
        _deleted = LoadTombstones();
        _writeTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(3) };
        _writeTimer.Tick += (s, e) => { _writeTimer.Stop(); WriteNow(); };
        _readTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1.5) };
        _readTimer.Tick += (s, e) => { _readTimer.Stop(); CheckNow(); };
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMinutes(1) };
        _pollTimer.Tick += (s, e) => CheckNow();

        _storage.Saved += OnLocalSaved;
        _storage.NoteDeleted += OnLocalDeleted;
        _storage.NoteRestored += OnLocalRestored;
    }

    public bool IsOn => !string.IsNullOrWhiteSpace(_settings.Settings.SyncFolder);

    public string? SyncDirectory => IsOn ? Path.Combine(_settings.Settings.SyncFolder, SubfolderName) : null;

    private string DeviceId
    {
        get
        {
            var s = _settings.Settings;
            if (string.IsNullOrWhiteSpace(s.SyncDeviceId))
            {
                s.SyncDeviceId = Guid.NewGuid().ToString("N");
                _settings.Save();
            }
            return s.SyncDeviceId;
        }
    }

    public string OwnFileName => $"notes-{Sanitize(Environment.MachineName)}-{DeviceId[..8]}.json";

    /// <summary>Starts (or restarts, after the folder changed) syncing; does nothing while sync is off.</summary>
    public void Restart()
    {
        StopWatching();
        LastError = null;
        if (!IsOn) return;
        try
        {
            Directory.CreateDirectory(SyncDirectory!);
            var s = _settings.Settings;
            if (!string.Equals(s.SyncBackedUpFor, s.SyncFolder, StringComparison.OrdinalIgnoreCase))
            {
                // a new folder may bring in notes from other PCs: keep this PC's notes as they were first
                DataBackupService.Snapshot("before-sync");
                s.SyncBackedUpFor = s.SyncFolder;
                _settings.Save();
            }
            _watcher = new FileSystemWatcher(SyncDirectory!, "notes-*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            _watcher.Changed += OnFolderChanged;
            _watcher.Created += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.EnableRaisingEvents = true;
            _pollTimer.Start();
            AppLog.Write($"Sync: watching {SyncDirectory}.");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppLog.Error("Starting sync", ex); // e.g. the NAS is offline: the minute check keeps trying
            _pollTimer.Start();
        }
        CheckNow();
        WriteNow();
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        if (string.Equals(e.Name, OwnFileName, StringComparison.OrdinalIgnoreCase)) return;
        _dispatcher.BeginInvoke(() => { _readTimer.Stop(); _readTimer.Start(); });
    }

    private void OnLocalSaved() => _dispatcher.BeginInvoke(() =>
    {
        if (!IsOn) return;
        _writeTimer.Stop();
        _writeTimer.Start();
    });

    private void OnLocalDeleted(Guid id, DateTime at)
    {
        lock (_deleted) _deleted[id] = at;
        SaveTombstones();
    }

    private void OnLocalRestored(Guid id)
    {
        lock (_deleted) _deleted.Remove(id);
        SaveTombstones();
    }

    /// <summary>Writes this PC's file now (only when something changed since the last write).</summary>
    public void WriteNow()
    {
        if (!IsOn) return;
        try
        {
            var snapshot = new SyncSnapshot
            {
                DeviceId = DeviceId,
                DeviceName = Environment.MachineName,
                WrittenAt = DateTime.Now,
                Notes = _storage.Notes.Where(n => !n.IsUntouchedSample).ToList(), // a new PC's welcome notes stay on it
                Deleted = CurrentTombstones().Select(kv => new SyncTombstone { Id = kv.Key, DeletedAt = kv.Value }).ToList(),
            };
            string body = JsonSerializer.Serialize(new { snapshot.Notes, snapshot.Deleted }, JsonOptions);
            if (body == _lastWritten) return;
            Directory.CreateDirectory(SyncDirectory!);
            AtomicFile.WriteAllText(Path.Combine(SyncDirectory!, OwnFileName), JsonSerializer.Serialize(snapshot, JsonOptions));
            _lastWritten = body;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppLog.Error("Writing to the sync folder", ex);
        }
    }

    /// <summary>Reads the other PCs' files that changed and hands the result to <see cref="PlanReady"/>.</summary>
    public void CheckNow()
    {
        if (!IsOn) return;
        try
        {
            if (!Directory.Exists(SyncDirectory)) return;
            var remotes = new List<SyncSnapshot>();
            bool anyNew = false;
            var files = Directory.GetFiles(SyncDirectory!, "notes-*.json")
                .Where(f => !string.Equals(Path.GetFileName(f), OwnFileName, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var file in files)
            {
                var stamp = File.GetLastWriteTimeUtc(file);
                if (!_seenFiles.TryGetValue(file, out var seen) || seen != stamp) anyNew = true;
                if (ReadSnapshot(file) is { } snapshot && snapshot.DeviceId != DeviceId)
                {
                    remotes.Add(snapshot);
                    _seenFiles[file] = stamp;
                }
            }
            OtherDevices = remotes.Select(r => r.DeviceId).Distinct().Count();
            LastSync = DateTime.Now;
            LastError = null;
            if (!anyNew || remotes.Count == 0) return;

            var plan = SyncMerge.Plan(_storage.Notes, CurrentTombstones(), remotes);
            if (!plan.IsEmpty)
            {
                AppLog.Write($"Sync: {plan.Added.Count} new, {plan.Updated.Count} changed, {plan.Deleted.Count} deleted on another PC.");
                PlanReady?.Invoke(plan);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppLog.Error("Reading the sync folder", ex);
        }
    }

    /// <summary>Another PC's file, or null when it can't be read right now (half-written, locked, damaged).</summary>
    public static SyncSnapshot? ReadSnapshot(string file)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<SyncSnapshot>(File.ReadAllText(file));
                if (snapshot == null || snapshot.Format != 1) return null;
                snapshot.Notes ??= new();
                snapshot.Deleted ??= new();
                return snapshot;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(150);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Unreadable sync file {Path.GetFileName(file)}", ex);
                return null;
            }
        }
        return null;
    }

    private Dictionary<Guid, DateTime> CurrentTombstones()
    {
        var oldest = DateTime.Now - SyncMerge.TombstoneLifetime;
        lock (_deleted)
        {
            foreach (var id in _deleted.Where(kv => kv.Value < oldest).Select(kv => kv.Key).ToList()) _deleted.Remove(id);
            return new Dictionary<Guid, DateTime>(_deleted);
        }
    }

    private static Dictionary<Guid, DateTime> LoadTombstones()
    {
        try
        {
            if (File.Exists(TombstoneFile))
            {
                var list = JsonSerializer.Deserialize<List<SyncTombstone>>(File.ReadAllText(TombstoneFile));
                if (list != null) return list.Where(t => t != null).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.Max(t => t.DeletedAt));
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Reading the list of deleted notes", ex);
        }
        return new Dictionary<Guid, DateTime>();
    }

    private void SaveTombstones()
    {
        try
        {
            var list = CurrentTombstones().Select(kv => new SyncTombstone { Id = kv.Key, DeletedAt = kv.Value }).ToList();
            AtomicFile.WriteAllText(TombstoneFile, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLog.Error("Saving the list of deleted notes", ex);
        }
    }

    private static string Sanitize(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));

    private void StopWatching()
    {
        _pollTimer.Stop();
        _readTimer.Stop();
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _seenFiles.Clear();
        _lastWritten = "";
    }

    public void Dispose()
    {
        if (_writeTimer.IsEnabled)
        {
            _writeTimer.Stop();
            WriteNow(); // edits from the last 3 seconds
        }
        StopWatching();
        _storage.Saved -= OnLocalSaved;
        _storage.NoteDeleted -= OnLocalDeleted;
        _storage.NoteRestored -= OnLocalRestored;
    }
}
