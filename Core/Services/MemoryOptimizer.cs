using System;
using System.Windows.Threading;

namespace SmartNotes.Core.Services;

public static class MemoryOptimizer
{
    private static DispatcherTimer? _trimTimer;

    public static void Initialize()
    {
        _trimTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMinutes(3)
        };
        _trimTimer.Tick += (s, e) => TrimMemory();
        _trimTimer.Start();
    }

    public static void TrimMemory()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized, false, false);
        }
        catch { }
    }
}
