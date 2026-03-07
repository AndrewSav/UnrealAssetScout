using System;
using System.IO;

namespace UnrealAssetScout.Logging;

// Resolves the default log file name and effective log file path for a run.
// Used by ConfigOptionsSupport for CLI help/defaults and by Program.Main before
// RuntimeLogging prepares or opens the file sink.
internal static class LogFilePathSupport
{
    internal static string ResolveLogFilePath(string logFile)
    {
        var path = string.IsNullOrWhiteSpace(logFile) ? GetDefaultLogFileName() : logFile;
        return Path.GetFullPath(path, Environment.CurrentDirectory);
    }

    internal static string GetDefaultLogFileName()
        => GetExecutableBaseName() + ".log";

    private static string GetExecutableBaseName()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var processName = Path.GetFileNameWithoutExtension(processPath);
            if (!string.IsNullOrWhiteSpace(processName)
                && !string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase))
                return processName;
        }

        var entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            return entryAssemblyName;

        return "unrealassetscout";
    }
}
