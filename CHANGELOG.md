# Changelog

## Unreleased

- `uas update` no longer reports a failure after it has already replaced the executable. The release response was disposed at the end of the run, after the swap had renamed the running executable away, and disposing it could then be the first thing to need an assembly the runtime had not loaded yet. The update had completed, but the run printed an error and exited non-zero.
- `uas update` now names the exception type when a failure carries no message, and appends the inner exception that the outer message defers to. The failure above reported nothing at all after the colon.
- `uas update` no longer ends in an unhandled exception when the connection times out, or when a file it replaces is held open by another process. Both are now reported like any other update failure.
- `export json` no longer loads a package it is going to skip. The skip list is now matched against the export map, so a package whose every export is on the list is never deserialized. Packages holding assets that are expensive to deserialize benefit most; an animation decompresses every curve and bone key before the skip list would otherwise see it. A package whose class names cannot all be resolved from the export map, a blueprint class among them, is loaded and decided exactly as before.
- A source skipped by the skip list no longer records its usmap types, dependencies or script bytecode state in the manifest. Such a source has never written output, so there is nothing for any of them to invalidate. It still records its export types, which is what the skip list diff reads, so relaxing `--skip-types` still re-exports everything it should. One consequence: flipping `--script-bytecode` now re-plans every skipped source. Those sources still write nothing, but a plan will count them as updates.
- `export json` no longer fails with "Insufficient memory to continue the execution of the program" on a package whose JSON is too large to hold in a single string. The JSON is now streamed to the output file instead of being built in memory first. Output of every package that exported before is unchanged. A package that fails part way through serialization no longer leaves a partial file behind. The export compatibility version is bumped, because an incremental run does not retry a package that failed before.
- Planning is faster on games that ship classic `.pak` files. The stored hash of every pak entry is now read by a pool of threads, sorted by offset within each pak, instead of one entry at a time. The gain is largest on a cold cache: an order of magnitude on an SSD, and a smaller one on an HDD. Fingerprints, and so plans and manifests, are unchanged.
- `export textures` no longer reports a texture that stores no image data as a failure. Render targets and media textures are drawn into while the game runs, so their cooked asset has no pixels to export; such a texture is now skipped quietly, and a package holding nothing else is no longer recorded as failed. Output is unchanged.
- The manifest header now lists only the settings the export mode reads. `skipTypes` and `scriptBytecode` appear only in `json` manifests, `audioDisambiguation` only in `audio` manifests, and the `usmap` block is left out in `simple` and `raw`, which load no packages. An existing manifest still loads and plans exactly as before, and nothing is re-exported.
- An export no longer processes a file once per container that holds it. A file shipped in both a base container and a patch container that replaces it was exported twice, base copy last, so the dump held the unpatched file. Each path is now exported once, from the container the game itself reads it from, and `list` shows it once.

## v0.5.0

- Normalise git commit hash in version to always be the same length
- Relaxed json string encoding in the manifest so that "0.0.0+5a3..." does not look "0.0.0\u002B5a3..."
- Updated CUE4Parse to the latest commit. This is a breaking change. Output may be different on the same input.
- Incremental runs now track textures written by models and animations exports, not just the meshes and materials.
- Added `uas licenses`, printing the license and the notices for everything bundled into the executable.
- Reworded the plan cost line an incremental run prints and removed unrecorded time handling, since it's always recorded.
- Texture, SVG and verse outputs are now named from the package and the export's outer chain rather than the export name alone, so two exports can no longer be written to the same file. Output layout changes only for packages that write more than one file; a package that writes one keeps its flat path. Audio keeps its existing path shape, because a Wwise or FMOD media name is already a full path identifying the media itself.
- An export run now warns at commit when one output file is written from more than one origin, naming every origin. Media reached by several events is one file and stays silent; different container entries landing on one name are reported.
- The documentation link printed by `--help` now points at the project GitHub page instead of a placeholder URL.
- Audio exports now suffix each Wwise media file with the id of the container it was read from, so media that CUE4Parse names alike no longer overwrite each other. `--no-audio-disambiguation` (`-d`) drops the suffix and reverts to stock FModel/CUE4Parse naming, keeping only the last-written of any media that collide.

## v0.4.0

- Upgrades will no longer produce a compatibility warning if the upgrade did not change export format.
- Switched to IncludeNativeLibrariesForSelfExtract=true for the releases.
- Reworked app version usage so it is consistent across display, logs and manifests.
- Every run logs its version as the first line, so a log file identifies the build that wrote it.
- Added `uas update` command that self-updates to the latest release.
- Fix `--help` and `--version` to write to stdout instead of stderr.
- Add installation script.

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

