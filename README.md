# UnrealAssetScout

CLI tool for listing and exporting Unreal Engine assets from game `.pak`/IoStore containers using CUE4Parse.

> Note: This project was written with help from [Claude Code](https://claude.ai/code) and [Codex](https://openai.com/codex/).

## Motivation and intended usage

Most people use [FModel](https://github.com/4sval/FModel) for extracting game assets from UE games, which is an excellent tool. I however wanted something that I can run quickly and repeatedly from the command line:

- Quickly - be able to filter out stuff I'm interested in so it does not spend time extracting stuff I do not care about
- Repeatedly - when a new version of a game comes out, I did not want it to be a big deal to re-run the export of the subset I require

Additionally, I wanted it to be reasonably useful as an exploration tool for new games without the overhead of the FModel UI. For some deeper analysis you might end up using FModel anyway, but I wanted something quick and dirty.

## Features

- List files in a game's `Paks` directory
- Filter files by regex (`--filter`)
- Filter package paths by cached export-type summaries (`--expression` with `list --format types` + `--types`)
- Export modes:
  - `simple` (standalone non-package files from pak/utoc containers; known formats are extracted or parsed, everything else is copied as-is)
  - `raw` (copy every matched file from pak/utoc containers byte-for-byte, including package companions such as `.uexp`, `.ubulk`, and `.uptnl`)
  - `json` (asset exports -> `.json`)
  - `textures` (textures and SVG assets -> `.png`)
  - `models` (conversion-backed meshes, skeletons, materials, and landscapes)
  - `animations` (conversion-backed animation assets)
  - `audio` (audio assets and MIDI)
  - `verse` (Verse digests -> `.verse`)

- Optional AES key support for encrypted containers (`--aes` or `--aes-file`)
- Optional usmap mappings support (`--usmap`) for games that require it
- Optional "mappings required" marker in output (`--mark-usmap`), so you know if you are missing out on some mappings

## CUE4Parse

UnrealAssetScout is built on top of [CUE4Parse](https://github.com/FabianFG/CUE4Parse), and the tool is subject to that library's capabilities and limitations.

- UnrealAssetScout only exports what CUE4Parse can parse and what this project has an exporter for.
- Some CUE4Parse-backed exporters can hit parser warnings or errors and still produce partial output or no output for a given asset/package. Those dependency logs are suppressed by default and can be enabled with `--log-libs` when needed for debugging.
- This repository uses CUE4Parse as a git submodule instead of a NuGet package because the interface changes rapidly.
- Because that interface changes rapidly, updating the submodule is likely to break UnrealAssetScout until the integration code here is adjusted to match.
- CUE4Parse has historically had Release-only build breaks, so the GitHub Actions publish workflow intentionally builds the Windows artifact with `Debug` configuration for reliability.

## Usage

```text
UnrealAssetScout [command] [options]

UnrealAssetScout list [options]
UnrealAssetScout export <mode> [options]
```

### Common options

- `-p`, `--paks` (required): Path to the game's `Paks` directory
- `-g`, `--game` (required): Value from CUE4Parse `EGame` enum (example: `GAME_UE5_4`), that indicate the Unreal Engine version the game uses. You can usually guess this value from the Details tab of Properties dialog of the game exe file.
- `-a`, `--aes`: AES-256 key (hex) for encrypted containers. Some games require an AES key for all relevant archives, some only for part of the content, and some not at all.
- `-j`, `--aes-file`: Path to a text file whose first line is the AES-256 key. If you pass both `--aes` and `--aes-file`, the file value wins.
- `-u`, `--usmap`: Path to `.usmap` file for unversioned assets. Some files can be read without mappings, while others need them for unversioned property decoding.
- `-f`, `--filter`: Regex path filter
- `-e`, `--expression`: Filter packages by their cached export summary using a boolean expression such as `UTexture and %exports > 1`. This only works together with `--types`. See below for more details about expression filtering.
- `-c`, `--types`: Path to a CSV produced by `list --format types`. UnrealAssetScout reads that file as cached package-summary data for `--expression`, instead of reloading every package just to decide what to include. This only works together with `--expression`. Warning: `--types` uses a previously generated `list --format types` CSV file as cached package summary data. It is your responsibility to regenerate that file whenever the game's pak/utoc contents change; otherwise `--expression` filtering can be stale or incorrect.
- `-s`, `--mark-usmap`: Prefix paths with `[ ]` or `[*]` for usmap requirement hints. This helps identify files that likely need mappings.
- `-r`, `--log-counter`: Prefix file-associated log lines in the log file with `[current/total]`. This only affects file logging, so it has no effect when `--no-log` is set. This is a debug-oriented option and is usually not needed; the exact details of how it works are not important and may change over time.
- `-l`, `--log`: Log file path (default: `.\<exe-name>.log`). This only matters when file logging is enabled.
- `-y`, `--log-append`: Append to the existing log file instead of overwriting it. This only matters when file logging is enabled.
- `-z`, `--no-log`: Disable file logging completely. If you also pass `--log`, `--log-append`, or `--log-counter`, those settings have no effect for that run.
- `-b`, `--log-libs`: Also log CUE4Parse warnings/errors and possibly other dependency warnings/errors. These external logs are suppressed by default so they do not pollute normal list output, especially CSV-oriented `list --format types` runs. This option is generally not recommended with `export`, where dependency noise can make progress output and failures harder to read.

These options are defined on the root command and inherited by both `list` and `export`.

### `list`

- `-t`, `--format`: `list:` Output format: `List`, `Tree`, or `Types`.
  - `List` is the default and prints matching file paths, optionally with `--mark-usmap`.
  - `Tree` renders a folder-only ASCII tree reconstructed from the mounted file paths. File leaves are omitted, so this shows directory structure only. Because the output contains folders only, `--mark-usmap` has no effect in this format
  - `Types` emits CSV with the header row `Path,Type,Count`. Files with no detected exports emit a single row with empty `Type` and `Count` fields. Files with exports emit one row per export type with that type's count. This is intended for external analysis so you can inspect archive type composition, derive useful filters, and identify which asset types matter for a game. This CSV is also the input for expression filtering: save it with `--file`, then pass it later through `--types` together with `--expression` to filter `list` or `export` runs by cached package composition. The `Types` format requires loading package files to inspect their exports, so list runs can be significantly slower than plain path listing. Because the `path` field is emitted as raw CSV data, `--mark-usmap` has no effect in this format.
  
- `-o`, `--file`: `list:` Also write the `list`, `tree`, or `types` output to a file while still keeping console output visible.

### `export`

- `<mode>`: `Simple`, `Raw`, `Json`, `Textures`, `Models`, `Animations`, `Audio`, or `Verse`

  - `Simple`: Processes only standalone non-package files from pak/utoc containers. Files with package-related extensions such as `.uasset`, `.umap`, `.uexp`, `.ubulk`, and `.uptnl` are skipped in this mode. Known formats are extracted or parsed; everything else is copied as-is. Companion files such as `.uexp`, `.ubulk`, or `.uptnl` may appear as skipped because they are usually read implicitly when the parent `.uasset` or `.umap` is processed.
  - `Raw`: Copies every matched file entry byte-for-byte to the output directory, including package files and companion files such as `.uasset`, `.umap`, `.uexp`, `.ubulk`, and `.uptnl`. No typed decoding or package-specific skipping is applied in this mode.
  - `Json`: Processes only `.uasset` and `.umap` packages and exports their asset data as `.json`. By default this mode skips the built-in type list documented below unless you override it with `--skip-types`, `--skip-types-file`, or disable it with `--no-skip-types`. While `.uexp`, `.ubulk`, and `.uptnl` may be surfaced as "skipped", they are usually companion data referenced by `.uasset` or `.umap` files and are processed as part of those packages. If world levels are present but not needed, using `--filter "^.*(?<!\.umap)$"` can significantly speed up export by excluding `.umap` packages.
  - `Textures`: Processes only `.uasset` and `.umap` packages and targets texture-style assets. Currently this means `UTexture` and `USvgAsset` exports. Some texture-like files may also exist as standalone non-package files, in which case they are extracted via `Simple` mode instead.
  - `Models`: Processes only `.uasset` and `.umap` packages and targets meshes, skeletons, materials, and landscapes. Currently this means `UMaterialInterface`, `USkeletalMesh`, `USkeleton`, `UStaticMesh`, and `ALandscapeProxy` exports.
  - `Animations`: Processes only `.uasset` and `.umap` packages and targets animation assets. Currently this means `UAnimSequence`, `UAnimMontage`, and `UAnimComposite` exports.
  - `Audio`: Processes only `.uasset` and `.umap` packages and targets audio and MIDI assets. Currently this means `UExternalSource`, `UAkAudioBank`, `UAkAudioEvent`, `UFMODEvent`, `UFMODBank`, `USoundAtomCueSheet`, `UAtomCueSheet`, `USoundAtomCue`, `UAtomWaveBank`, `UAkMediaAsset`, `UAkAudioEventData`, `UMidiFile`, `USoundWave`, and `UAkMediaAssetData` exports. Some audio files may also exist as standalone non-package files, in which case they are extracted via `Simple` mode instead.
  - `Verse`: Processes only `.uasset` packages and targets Verse digest assets. Currently this means `UVerseDigest` exports.

- `-o`, `--output` (required): `export:` Output directory
- `-v`, `--verbose`: `export:` Log skipped files
- `-x`, `--compact`: `export:` Compact progress display. When file logging is enabled, detailed logs still go to the log file; with `--no-log`, you only get the compact console progress output.
- `-t`, `--skip-types`: `export json:` Replaces the built-in skip list with the specified type names, using normal whitespace-separated command-line values. Unless a higher-precedence skip-type option is also present. See `Default JSON Skip Types` below for the built-in list this replaces.
- `-w`, `--skip-types-file`: `export json:` Path to a text file containing skip type names. Commas and whitespace are treated as separators, so the file can use one type per line, multiple space-separated types on a line, or comma-separated entries. If both `--skip-types` and `--skip-types-file` are present, the file wins. See `Default JSON Skip Types` below for the built-in list this overrides.
- `-k`, `--no-skip-types`: `export json:` Disable the built-in skip list entirely. See `Default JSON Skip Types` below for the built-in list this disables. This has the highest precedence and overrides both `--skip-types-file` and `--skip-types`.
- `-d`, `--script-bytecode`: `export json:` Serialize Unreal script bytecode into JSON output when available. This is ignored for other export modes.

Mode values are parsed case-insensitively, so `export json` and `export Json` both work.

### Response file

- Use `@file` to inline arguments from a response file.
- Command tokens can live inside response files, for example `export json`.
- One switch per line is recommended.
- Quotes are supported.
- Lines starting with `#` are comments.

### Expression Filtering

`--expression` is a package-level filter. It does not inspect raw file paths or file extensions directly. Instead, it evaluates a cached summary of each package's export composition from a CSV produced by `list --format types`.

The normal workflow is:

1. Run `list --format types` and save the CSV with `--file`.
2. Reuse that CSV with `--types`.
3. Pass `--expression` to keep only the package paths whose cached export summary matches.
4. Use the filtered path set with either `list` or `export`.

Example workflow:

```powershell
uas.exe `
  list `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --format types `
  --file "D:\Analysis\types.csv"

uas.exe `
  export json `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --types "D:\Analysis\types.csv" `
  --expression "UTexture and %exports > 1" `
  --output "D:\Export\Json"
```

What `--types` file contains:

- One CSV row per detected export type per package path.
- Columns are `Path,Type,Count`.
- Paths with no detected exports still emit one row with empty `Type` and `Count`.

### Expression Syntax

Expressions are built from three kinds of values:

- Type names, that game packages export, such as `UTexture` or `USoundWave`
- Special keywords:  `%exports` and `%types`
- Numbers such as `1`, `3`, or `20`

A simple expression can be just a type name on its own, or a comparison such as `USoundWave >= 3` or `%exports > 10`. More complex expressions combine those pieces with `and`, `or`, `not`, and parentheses.

Type names used on their own:

- A type name by itself means "this package contains at least one export of that type".
- Example: `UTexture` means `UTexture > 0`.
- Type name matching is case-insensitive.

Supported keywords:

- `%e` or `%exports`: total number of exports in the package
- `%t` or `%types`: number of distinct export types in the package

Supported operators:

- Comparison: `=`, `!=`, `>`, `<`, `>=`, `<=`
- Boolean AND: `and`, `&`, `&&`
- Boolean OR: `or`, `|`, `||`
- Boolean NOT: `not`, `!`, `~`
- Parentheses for grouping: `(` and `)`

Supported expression forms:

- Bare type presence: `USoundWave`
- Type-count comparison: `USoundWave >= 3`
- Keyword comparison: `%exports > 10`
- Mixed comparisons: `UTexture and %types >= 2`
- Nested boolean expressions: `(UTexture or USvgAsset) and not USoundWave`

Examples:

- `UTexture`
  Matches packages containing at least one `UTexture` export.
- `USoundWave >= 3`
  Matches packages containing three or more `USoundWave` exports.
- `%exports > 20`
  Matches packages with more than twenty total exports.
- `%types >= 4`
  Matches packages containing at least four distinct export types.
- `UTexture and USoundWave`
  Matches packages containing both `UTexture` and `USoundWave`.
- `UTexture and %exports > 1`
  Matches packages containing textures and more than one total export.
- `(UTexture or USvgAsset) and not USoundWave`
  Matches texture-oriented packages while excluding ones that also contain `USoundWave`.
- `2 < %exports`
  Comparisons can be written with the number on the left as well.

Practical notes:

- `--expression` and `--types` must be supplied together. Neither option does anything useful on its own.
- The filtering is only as current as the CSV passed to `--types`. If the game updates, regenerate the CSV.
- `--filter` and `--expression` can be combined. A file must pass both to be processed.

### Default JSON Skip Types

These are the built-in types skipped by `export json` when you do not override the behavior with `--skip-types` or `--skip-types-file`, and do not disable it with `--no-skip-types`.

- `UTexture`
- `USvgAsset`
- `UExternalSource`
- `UAkAudioBank`
- `UAkAudioEvent`
- `UFMODEvent`
- `UFMODBank`
- `USoundAtomCueSheet`
- `UAtomCueSheet`
- `USoundAtomCue`
- `UAtomWaveBank`
- `UAkMediaAsset`
- `UAkAudioEventData`
- `USoundWave`
- `UAkMediaAssetData`
- `UAnimSequenceBase`
- `UAnimMontage`
- `UAnimComposite`
- `USkeletalMesh`
- `UStaticMesh`
- `USkeleton`
- `ALandscapeProxy`
- `UMidiFile`

## Examples

List files:

```powershell
uas.exe `
  list `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --mark-usmap
```

List folders as an ASCII tree:

```powershell
uas.exe `
  list `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --format tree
```

List files as CSV rows for export-type analysis:

```powershell
uas.exe `
  list `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --format types
```

List files as CSV rows and save them for later expression-based filtering:

```powershell
uas.exe `
  list `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --format types `
  --file "D:\Analysis\types.csv"
```

Export only paths whose cached type summary matches an expression:

```powershell
uas.exe `
  export json `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --types "D:\Analysis\types.csv" `
  --expression "UTexture and %exports > 1" `
  --output "D:\Export\Json"
```

Export JSON for matching paths:

```powershell
uas.exe `
  export json `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --filter "Content/UI" `
  --output "D:\Export\Json"
```

Export non-package files from inside containers:

```powershell
uas.exe `
  export simple `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --output "D:\Export\Raw"
```

Export model assets with AES key:

```powershell
uas.exe `
  export models `
  --paks "C:\Game\Content\Paks" `
  --game GAME_UE5_3 `
  --aes 0xYOUR64BYTEHEXKEY `
  --output "D:\Export\Models"
```
