using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SmartNotes.Core.Services;

public static class MemoryOptimizer
{
    private static DispatcherTimer? _trimTimer;

    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    public static void Initialize()
    {
        _trimTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _trimTimer.Tick += (s, e) => TrimMemory();
        _trimTimer.Start();

        // Schedule an immediate post-startup trim once all initial windows and JIT are loaded
        ScheduleDelayedTrim(TimeSpan.FromSeconds(2));
    }

    public static void ScheduleDelayedTrim(TimeSpan delay)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = delay
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            TrimMemory();
        };
        timer.Start();
    }

    public static void TrimMemory()
    {
        try
        {
            // 1. Compact Large Object Heap and run comprehensive GC
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

            // 2. Instruct Windows OS to trim unreferenced physical working set pages
            if (OperatingSystem.IsWindows())
            {
                using var process = Process.GetCurrentProcess();
                SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
        }
        catch { }
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
