# Changelog

## Unreleased

- Switched to IncludeNativeLibrariesForSelfExtract=true for the releases.
- Reworked app version usage so it is consisten across display, logs and manifests.
- Every run logs its version as the first line, so a log file identifies the build that wrote it.

## v0.3.0

- Incremental export. When a manifest is present in the output directory, an export run only
  redoes work whose inputs changed, and deletes outputs no longer produced. `--rebuild` forces a
  full run, `--dry-run` reports without writing, and `--accept-tool-version` proceeds past a uas
  or CUE4Parse version change, accepting that output may then not match a full rebuild.
- Short option letters reassigned so they are mnemonic, using capitals where a related pair exists:
  `-A` aes-file, `-T` types, `-m` mark-usmap, `-i` log-counter, `-L` log-append, `-D` log-libs,
  `-F` list format, `-c` compact, `-s` skip-types, `-S` skip-types-file, `-b` script-bytecode, and
  new `-r` rebuild, `-n` dry-run, `-q` accept-tool-version. Long options are unchanged.
- Errors are written to standard error under `--compact`. Compact progress previously replaced
  all console output, so a run that stopped reported only a non-zero exit code, and with
  `--no-log` the reason was not recorded anywhere.
- The test suite runs before a release is published, so a tag cannot be cut from a commit whose
  tests fail.

## v0.2.1

- A package is skipped only when every one of its exports is specialized, and is otherwise written in full.
- Removed the `[FILTERED]` `--verbose` line, which no longer has anything to report.

## v0.2.0

- Fixed `export json` skip-list entries that name a base class never matching anything.
- Fixed `export json` discarding an entire package when only some of its exports matched the skip list.
- `--verbose` now also reports `[FILTERED]` for packages that are written with some exports dropped.
- Made `list --format types` much faster.
- Upgrade the project to .NET 10.
- Updated the bundled CUE4Parse.
- Published binaries are now built in release configuration instead of debug.

## v0.1.0

- Renamed export modes to align with FModel terminology: `graphics` became `textures`, and `spatial` was split into `models` and `animations`. The new `models` mode exports meshes, skeletons, materials, and landscapes, while `animations` exports animation assets only.
- Added `export --script-bytecode` to include Unreal script bytecode in JSON exports when supported. The flag is accepted for all export modes but only affects `json`.
- Fixed detex initialization for texture exports and aligned its startup setup with the existing zlib and oodle native dependency initialization near `uas.exe`.
- Fixed CLI version reporting so `--version` works without requiring export/list options, and set local dev builds to default to assembly version `0.0.0.0`.

## v0.0.1

- Initial release

