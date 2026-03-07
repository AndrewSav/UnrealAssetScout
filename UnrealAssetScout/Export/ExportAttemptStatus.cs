namespace UnrealAssetScout.Export;

// Categorizes exporter outcomes so ExportProcessor can distinguish real failures from "not handled"
// cases that should quietly fall back to raw extraction or other processing paths.
internal enum ExportAttemptStatus
{
    NotHandled,
    Succeeded,
    Failed
}
