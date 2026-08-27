using System;
using System.IO;
using System.Reflection;

namespace UnrealAssetScout.Licensing;

// Writes the license of uas itself followed by the notices for everything redistributed inside
// the executable. Called by ConfigOptionsSupport when the parsed command is `licenses`.
// Both texts are embedded resources rather than files on disk because a release is a single
// executable that the installer copies on its own, leaving nothing else to read.
internal static class LicensesCommand
{
    internal static int Run()
    {
        Console.Out.WriteLine(Read("LICENSE").TrimEnd());
        Console.Out.WriteLine();
        Console.Out.WriteLine(Read("THIRD-PARTY-NOTICES").TrimEnd());
        return 0;
    }

    private static string Read(string resourceName)
    {
        var qualifiedName = $"UnrealAssetScout.{resourceName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(qualifiedName)
                           ?? throw new InvalidOperationException($"{qualifiedName} is not embedded in this build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
