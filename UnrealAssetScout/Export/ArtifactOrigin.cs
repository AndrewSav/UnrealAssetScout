namespace UnrealAssetScout.Export;

// Where an exported file's bytes came from: the container entry that holds them, and, when that
// entry holds more than one, which part of it. Created by exporters that read from a container,
// such as AudioExporter reading Wwise media, and compared by DuplicateOutputCheck to tell one file
// reached by several sources from two different data landing on one name.
// Container is reported on its own in warnings because it is the path the same bytes have in a
// simple-mode dump, which is where a reader goes to find both copies.
internal readonly record struct ArtifactOrigin(string Container, string? WithinContainer)
{
    internal string Key => WithinContainer is null ? Container : $"{Container}/{WithinContainer}";
}
