using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// One usmap type's semantic identity for closure walking: its supertype and the struct and enum
// types its own properties reference. Built by UsmapFingerprints and traversed by UsmapClosure.
internal sealed record UsmapTypeNode(
    string Name,
    string? Super,
    IReadOnlyList<string> ReferencedTypes,
    IReadOnlyList<string> ReferencedEnums);
