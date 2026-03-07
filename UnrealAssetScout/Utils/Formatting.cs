using System;

namespace UnrealAssetScout.Utils;

// Static utility class with time-formatting helpers. Used by Program.Main for the final
// elapsed/stats output and by CompactProgress for the timing columns in the progress bar.
internal static class Formatting
{
    internal static string FormatElapsed(TimeSpan elapsed)
        => $"{(int) elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    internal static string FormatMilliseconds(double milliseconds)
    {
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0)
            return "n/a";

        var duration = TimeSpan.FromMilliseconds(milliseconds);

        if (duration.TotalSeconds < 1)
            return $"{milliseconds:0.###} ms";

        if (duration.TotalMinutes < 1)
            return $"{duration.TotalSeconds:0.###} s";

        if (duration.TotalHours < 1)
        {
            var minutes = (int) duration.TotalMinutes;
            var seconds = duration.TotalSeconds - (minutes * 60);
            return $"{minutes}m {seconds:00.###}s";
        }

        var hours = (int) duration.TotalHours;
        return $"{hours}h {duration.Minutes:00}m {duration.Seconds:00}s";
    }
}
