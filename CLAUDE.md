# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commit message style

- Start all commit messages with a lowercase letter.

## Pull request descriptions

- When authoring a PR for this repository, include a note in the PR description that it was authored by Claude.

## Release preparation

- When preparing a release, first verify that the current branch is the tip of `main` and that the workspace is clean. If either check fails, stop and warn instead of making changes.
- When preparing a release, if the release version is not explicitly provided, ask for it instead of assuming.
- When preparing a release, run a local `dotnet build UnrealAssetScout.slnx` before making changelog changes to confirm the repo is in a buildable state.
- When preparing a release, leave the default local `Version` and `AssemblyVersion` at `0.0.0.0`; do not bump them for releases.
- When preparing a release, after moving entries out of `Unreleased`, leave a single bullet under `## Unreleased` that says `None`.

## Line endings

- Do not introduce mixed line endings when editing files.
- Preserve the file's existing line-ending style, and normalize the whole file if an edit would otherwise leave it mixed.

## File encoding

- Save source and project files as UTF-8 with a BOM: `.cs`, `.csproj`, `.slnx`, `.sln`, `.ps1`, `.DotSettings`. These can legitimately contain non-ASCII characters, and the BOM stops tooling from having to guess the encoding.
- Save documentation, plain-text, and config files without a BOM: `.md`, `.txt`, `.yml`, `.json`, `.gitignore`.
- Keep those non-BOM files ASCII-only. Do not use em dashes, en dashes, curly quotes, ellipsis characters, or any other non-ASCII punctuation; use plain ASCII equivalents instead.

## CUE4Parse boundaries

- `CUE4Parse` is an external library and does not belong to this project.
- Do not modify code in `CUE4Parse`.
- Do not run tests against `CUE4Parse`.
- `CUE4Parse` is included as a git submodule because it changes more frequently than NuGet packages are published, so this repo can stay up to date.
- Focus most implementation effort outside `CUE4Parse`.
- It is fine to inspect `CUE4Parse` code when it helps implement or debug this project.

## Logging split

- UnrealAssetScout application logs must go through `UnrealAssetScout.Logging.AppLog`, not `Serilog.Log`.
- The global Serilog logger is reserved for dependency logging such as `CUE4Parse`, and is suppressed by default unless `--log-libs` is enabled.
- If new UnrealAssetScout code logs through `Serilog.Log`, those logs may be hidden by default runs.


## Classes comments

We keep each top level class in the UnrealAssetScout project commented at the top, except for the main Program class. The class comment should explain the main purpose of the class, and explain where the class is used. Example:

```
// A Serilog sink that counts warnings and errors as they flow through the logging pipeline.
// Created by RuntimeLogging.ReConfigureLogger when compact progress is enabled, returned to
// Program.Main, and passed to CompactProgress to display live warn/error counts in the progress bar.
internal sealed class LogLevelCounterSink : ILogEventSink
```

Every time your change code, make sure that class comments remain up to date.

## More local knowledge

Several external sources are useful when working on this project. They are not part of this repo and
are not vendored into it; check for them as siblings of this repo on the local file system
(alongside it, not inside it) before assuming they are unavailable.

These are git repositories. If one is needed and absent, ask for it to be cloned:

- FModel is an asset extraction GUI and has a lot of code paths and code that is relevant to this project and can be reused, or used to gain insight on some of this project parts
- command-line-api - System.CommandLine source code
- superpower - Datalust Superpower source code
