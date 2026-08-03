# Code style

Conventions for the `UnrealAssetScout` and `UnrealAssetScout.Tests` projects. `CUE4Parse` is an
external submodule and is not covered by any of this.

## One type per file

Each file declares exactly one top-level type, and the file is named after it. This applies to
classes, records, record structs and enums alike.

Nested types do not count and are not affected. A type used only by its enclosing type belongs
inside it, not in a file of its own.

Small types get their own file too. Being small is not a reason to be hard to find: a three-value
enum such as `ExportAttemptStatus` gets a file like anything else.

Declare a second top-level type only when an external constraint leaves it nowhere else to live,
such as an xUnit `[CollectionDefinition]` marker class, and comment why.

## Class header comments

Every top-level class in the UnrealAssetScout project carries a comment at the top, except the
main `Program` class. The comment explains the main purpose of the class, and where the class is
used. Example:

```csharp
// A Serilog sink that counts warnings and errors as they flow through the logging pipeline.
// Created by RuntimeLogging.ReConfigureLogger when compact progress is enabled, returned to
// Program.Main, and passed to CompactProgress to display live warn/error counts in the progress bar.
internal sealed class LogLevelCounterSink : ILogEventSink
```

This is the one comment that is always expected.

Keep these up to date when the class changes. A header that describes what the class used to do is
worse than none, because it is trusted.

## Every other comment

Comments outside class headers are rare. Write one only to record something the code cannot state
itself:

- **A fact about an external system.** That `bHasVersioningInfo` is the first `uint32` of a UE5
  IoStore package, because nothing in the code says why reading four bytes answers the question.
- **Why a call exists at all.** That `ZlibHelper.Initialize` is needed for CUE4Parse to download its
  zlib and oodle binaries, or that submitting a key for the zero GUID is what triggers mounting.
  Both look removable without the comment.
- **A boundary condition a bug turned up.** That an empty skip list or an empty package is never
  skipped, which is the behaviour a fix had to preserve.
- **A consequence that is easy to miss.** That matching base types means listing `UObject` would
  skip every export.
- **Why the obvious approach was rejected.** That class names are resolved from the export map
  rather than via `UObject.ExportType` because the latter forces the lazy export to deserialize.

Do not write a comment that restates what the code does, names the steps a method already names, or
explains a language feature. If the code needs that, rename something instead.

A bug fix that turned on a subtlety is the usual reason a new comment is justified: record the
reasoning that makes the fix correct.

In tests, comment only to explain why a particular fixture was chosen, never the mechanics of the
test.

## Naming

Use full words. Multi-word identifiers are normal and expected: `inlineSkipTypesSpecified`,
`nonPackageFileLogContext`, `trackUsmapRequiredCount`, `loadFailureRequirement`.

Do not abbreviate. Write `index` not `idx`, `context` not `ctx`, `message` not `msg`, `options` not
`opts`, `previous` not `prev`, `configuration` not `cfg`. No domain object is named with a short
identifier.

Four exceptions:

1. **Lambda and loop parameters, and caught exceptions**, may be single letters: `e`, `i`, `lc`, `g`.
   Anything that outlives a couple of lines gets a real name.
2. **File format and acronym names are names, not abbreviations**: `svg`, `csv`, `wem`, `bnk`, `acb`,
   `awb`. Do not expand them.
3. **At the CUE4Parse boundary, mirror CUE4Parse's own naming.** `PackageLoadSupport` uses `ar` and
   `pkg` because that is what CUE4Parse calls them, and matching it makes the two sides easy to read
   together. This applies only to code sitting directly on that seam, not to the rest of the project.
4. **`Dir` and `stats`** appear in `outputDir`, `exeDir`, `runStats` and `modeStats`. They are
   tolerated where they already exist. Prefer the full word in new code.

## See also

`CLAUDE.md` covers line endings, file encoding, commit message style, release preparation, the
CUE4Parse submodule boundary, and the logging split between `AppLog` and `Serilog.Log`.
