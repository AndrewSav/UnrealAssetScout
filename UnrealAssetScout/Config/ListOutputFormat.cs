namespace UnrealAssetScout.Config;

// The available output formats for the `list` command.
// Parsed by ConfigOptionsSupport for list-mode runs, then consumed by ListProcessor to choose
// between plain file listing, folder-tree output, and CSV export-type analysis output.
internal enum ListOutputFormat
{
    List,
    Tree,
    Types
}
