namespace UnrealAssetScout.Export;

// Identifies the supported export pipelines selectable from command-line options.
// Parsed by ConfigOptionsSupport, consumed by Program.Main, and dispatched by ExportProcessor to
// route each run through the appropriate export mode implementation.
public enum ExportMode
{
    Simple,
    Raw,
    Json,
    Textures,
    Models,
    Animations,
    Audio,
    Verse
}
