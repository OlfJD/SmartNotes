using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SmartNotes.Core.Services;

public static class MemoryOptimizer
{
    private static DispatcherTimer? _trimTimer;
    private static bool _autoTrimEnabled = true;

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    public static bool AutoTrimEnabled
    {
        get => _autoTrimEnabled;
        set
        {
            _autoTrimEnabled = value;
            if (_trimTimer != null)
            {
                if (_autoTrimEnabled) _trimTimer.Start();
                else _trimTimer.Stop();
            }
        }
    }

    public static void Initialize(bool autoTrimEnabled = true)
    {
        _autoTrimEnabled = autoTrimEnabled;
        _trimTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMinutes(3)
        };
        _trimTimer.Tick += (s, e) =>
        {
            if (_autoTrimEnabled)
            {
                TrimMemory();
            }
        };

        if (_autoTrimEnabled)
        {
            _trimTimer.Start();
        }
    }

    /// <summary>
    /// Forces full GC compaction and flushes unused working set pages back to Windows via EmptyWorkingSet.
    /// Returns (BeforeMb, AfterMb, FreedMb).
    /// </summary>
    public static (double BeforeMb, double AfterMb, double FreedMb) TrimMemory()
    {
        double beforeMb = GetCurrentMemoryUsageMb();
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch { }

        double afterMb = GetCurrentMemoryUsageMb();
        double freedMb = Math.Max(0, beforeMb - afterMb);
        return (beforeMb, afterMb, freedMb);
    }

    public static double GetCurrentMemoryUsageMb()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            return process.WorkingSet64 / (1024.0 * 1024.0);
        }
        catch
        {
            return 0.0;
        }
    }
}
