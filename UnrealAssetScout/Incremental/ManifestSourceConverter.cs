using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// Writes one manifest source entry per line while the rest of the manifest stays indented.
// Registered on ExportManifestStore's serializer options.
// The global block at the top is what a person reads when reviewing how a run was configured, and
// indenting it is what makes that possible. Indenting the source entries as well would bury that
// block under millions of lines and roughly double the file, while one line per source stays
// readable and greppable.
internal sealed class ManifestSourceConverter : JsonConverter<ManifestSource>
{
    // Deliberately a separate options instance with this converter absent: reusing the caller's
    // would recurse, and it is also what turns the indentation off for the entry itself.
    private static readonly JsonSerializerOptions EntryOptions = new() { WriteIndented = false };

    public override ManifestSource? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<ManifestSource>(ref reader, EntryOptions);

    public override void Write(Utf8JsonWriter writer, ManifestSource value, JsonSerializerOptions options) =>
        writer.WriteRawValue(JsonSerializer.Serialize(value, EntryOptions));
}
