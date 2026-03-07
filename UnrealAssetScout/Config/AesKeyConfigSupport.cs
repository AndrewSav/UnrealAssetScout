using System;
using System.IO;
using System.Linq;
using UnrealAssetScout.Logging;

namespace UnrealAssetScout.Config;

// Resolves AES key configuration from the dedicated key-file option.
// Called by ConfigOptionsSupport while building Options before provider setup in Program.Main.
internal static class AesKeyConfigSupport
{
    internal static bool TryReadKeyFile(string rawValue, out string resolvedValue)
    {
        var keyFilePath = Path.GetFullPath(rawValue);
        if (!File.Exists(keyFilePath))
        {
            AppLog.Error("AES key file not found: {Path}", keyFilePath);
            resolvedValue = string.Empty;
            return false;
        }

        try
        {
            resolvedValue = File.ReadLines(keyFilePath).First().Trim();
            return true;
        }
        catch (Exception e)
        {
            AppLog.Error("Failed to read AES key file '{Path}': {Message}", keyFilePath, e.Message);
            resolvedValue = string.Empty;
            return false;
        }
    }
}
