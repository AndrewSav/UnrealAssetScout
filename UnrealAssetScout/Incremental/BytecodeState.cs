namespace UnrealAssetScout.Incremental;

// The three values ManifestSource.B can hold. Written by SourceRecorder and read by
// ExportPlanner. "false" is the only state safe to skip on a --script-bytecode flip, and is only
// ever set by direct observation during an export.
internal static class BytecodeState
{
    internal const string True = "true";
    internal const string False = "false";
    internal const string Unknown = "unknown";
}
