using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CUE4Parse.UE4.Assets.Exports;
using Newtonsoft.Json;

namespace UnrealAssetScout.Export.Exporters;

// Owns JSON-mode export helpers and package JSON serialization.
// Called by JsonPackageProcessor to decide whether a package should be skipped or written as JSON.
// The JSON is streamed to disk rather than built in memory first, because a single .NET string has
// a size ceiling that the JSON of some packages exceeds.
internal static class PackageJsonExporter
{
    // Matches File.WriteAllText, which wrote this output before it was streamed: no byte order mark,
    // and invalid UTF-16 rejected rather than silently replaced.
    private static readonly UTF8Encoding Utf8NoBomStrict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static ExportAttemptResult TryExport(string path, string outputDir, IReadOnlyCollection<UObject> exports)
    {
        var outPath = ExportPathUtils.ToOutputPath(outputDir, path, ".json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var textWriter = new StreamWriter(outPath, append: false, Utf8NoBomStrict, bufferSize: 65536))
            using (var jsonWriter = new JsonTextWriter(textWriter) { Formatting = Formatting.Indented })
            {
                var serializer = JsonSerializer.CreateDefault();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(jsonWriter, exports);
            }

            return ExportAttemptResult.Success(path, outPath);
        }
        catch (Exception e)
        {
            DeletePartialOutput(outPath);
            return ExportAttemptResult.Failure(path, e.Message);
        }
    }

    // A failed package records no output in the manifest, so a partly written file would never be
    // cleaned up by a later run and would read as a complete export.
    private static void DeletePartialOutput(string outPath)
    {
        try
        {
            File.Delete(outPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
