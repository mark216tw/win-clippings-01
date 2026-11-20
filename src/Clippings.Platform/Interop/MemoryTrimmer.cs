using System.Runtime;

namespace Clippings.Platform.Interop;

public static class MemoryTrimmer
{
    private static int _scheduleVersion;

    public static void ScheduleTrim(int delayMilliseconds = 400, Func<bool>? shouldTrim = null)
    {
        var version = Interlocked.Increment(ref _scheduleVersion);
        _ = Task.Run(async () =>
        {
            try
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                }

                if (Volatile.Read(ref _scheduleVersion) != version)
                {
                    return;
                }

                if (shouldTrim is not null && !shouldTrim())
                {
                    return;
                }

                TrimNow();
            }
            catch (Exception)
            {
            }
        });
    }

    public static void TrimNow()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        _ = NativeMethods.SetProcessWorkingSetSize(
            NativeMethods.GetCurrentProcess(),
            new IntPtr(-1),
            new IntPtr(-1));
    }
}
