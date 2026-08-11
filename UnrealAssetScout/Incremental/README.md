# Incremental export

## What it does

After a game patch, an export run leaves the output folder in exactly the state a from-scratch
run with the same options would produce, without redoing work whose inputs are unchanged: old
assets removed, unchanged assets left untouched, new assets added, changed assets updated. It is
automatic whenever `<output>/.uas-manifest.json` exists from a previous run; `--rebuild` forces a
full run and replaces the manifest.

The correctness bar is **bit-exact**: an incremental run and a from-scratch run with the same
inputs must produce identical output files. That bar is relaxed in exactly two ways, both
explicit rather than accidental:

- An accepted tool change. `--accept-tool-version` carries outputs produced under a different
  export-behaviour number or a different CUE4Parse commit forward unchanged, trading bit-exactness
  for not re-running everything. The dump is then a known mixture, and every run says so. An
  upgrade that does not change exporting is not such a change and needs no override.
- Untracked files. Anything in the output tree the manifest never recorded is left alone. A
  from-scratch run would not produce such a file, so its presence is the one way an incremental
  dump can differ from a full one without anyone having opted in.

It covers every export mode uniformly, json, textures, models, animations, audio, verse, simple
and raw, because every mode already reports its artifacts through the same shape. `list` modes
write no dump and are unaffected.

## The three phases and the file map

```
PLAN                          EXECUTE                        COMMIT
gate, fingerprint, diff   ->  ExportProcessor over       ->  delete orphans
usmap, propagate layouts      the work list only             write manifest atomically, last
-> ExportPlan                 record artifacts + deps
```

`IncrementalRunner.Run` is the entry point, called from `Program.Main` in place of a bare
`ExportProcessor.ProcessFiles` call. It drives all three phases; when investigating a symptom,
start there and follow the phase below.

### PLAN

Decides what needs exporting. Touches disk and the provider to gather facts, but the decision
itself (`ExportPlanner.Plan`) is a pure function over plain data: no disk, no provider, no
CUE4Parse. That purity is what makes ten separate invalidation rules unit-testable without
mounting a game, and it is the main defence against the failure mode that matters, a wrongly
skipped package leaving a stale file nobody notices.

| File | Role |
|---|---|
| `ExportManifestStore`, `ManifestSourceConverter` | Loads the previous manifest; a load failure here, unless `--rebuild`, is fatal. The converter is what keeps source entries to one line each when saving |
| `SourceSetBuilder` | Builds S, the current source set, from resolved provider paths, the mode's extension rules, `--filter` and the type filter |
| `SourceFingerprintIndex` | Path to stored hash, for every resolved container entry |
| `PakInlineHeaderFingerprints`, `PakInlineHeaderLayout` | Reads the SHA-1 a pak entry's inline header already carries |
| `IoStoreTocFingerprints` | Re-reads `.utoc` metadata to map an IoStore chunk to its stored hash |
| `UsmapFingerprints`, `UsmapSnapshot`, `UsmapTypeNode` | Reduces a loaded usmap to per-type and per-enum semantic fingerprints plus a reference graph |
| `UsmapClosure` | Expands a recorded type name into everything reachable from it, memoised |
| `ExportCompatibility`, `ToolVersionPair` | What a dump was produced by, for the tool gate: this build's export-behaviour number and the pinned CUE4Parse commit |
| `PlanInputs`, `PlanResult`, `ExportPlan`, `SourceCandidate` | The planner's input and output shapes |
| `ExportPlanner` | The gate, the direct staleness rules, propagation to a fixpoint, and the work list / carry-forward split |
| `StaleReason`, `PlanStatistics` | Which rule first marked each source stale, and the counts and previous cost behind the summary lines a run prints |

If output is missing or stale for something that should have invalidated it, or present when it
should not be, `ExportPlanner` is where the rule lives or is missing. If a run refuses to proceed
at all, the gate at the top of `ExportPlanner.Plan` is where the condition is checked.

**Gate conditions**, checked before anything else and in this order:

| Condition | Result |
|---|---|
| No manifest | Full run |
| `--rebuild` | Full run, manifest replaced |
| Manifest present but unparseable, or its `schema` does not match this build's | Error, stop, unless `--rebuild` |
| `mode` mismatch | Error, stop |
| `game` mismatch | Error, stop |
| A container recorded in the manifest is not currently mounted | Error, stop |
| Current tool version pair not recorded in the manifest | Error, stop, unless `--accept-tool-version` |

A missing manifest is the only condition that implicitly runs full. Everything else that is wrong
stops and names the actual mismatch rather than silently falling back to a full rebuild.

**Direct staleness**, checked per source once the gate passes:

- Absent from the previous manifest (a novel path)
- The constituent set differs from what was recorded, a file was added or removed
- Any constituent's fingerprint differs from what was recorded
- Any recorded dependency's fingerprint differs from what it resolves to now (present on exactly
  one side counts as changed; absent on both sides does not)
- A tracked output is missing from disk
- The external-media flag `e` is set
- The `--script-bytecode` effective value flipped and the recorded `b` is not `false`
- The skip predicate's verdict, evaluated against the recorded `x`, differs between the recorded
  skip set and the current one
- The recorded consulted-types set (`t`) intersects the closure of everything that changed in the
  usmap
- Any recorded blocked name (`u`) is now present in the current usmap's types or enums

A source directly stale by any of these, or reachable from one by staleness propagation, joins
the work list; everything else carries forward. The dependency rule above must stay eager over
every import, layout provider or not: a rendered reference carries the target package's export
index, and any content change in the target can shift its export table and therefore this
package's output while its own bytes stay untouched. That was observed in the wild, and an
attempt to narrow the rule to layout providers was falsified by the end-to-end protocol on its
first filtered run. Propagation, which only needs to carry staleness beyond that first hop, runs
over reverse layout-provider edges (`p`): a package renders indices only for its own imports, so
past one hop only an embedded layout can transmit a change. The `models` and `animations` modes
are the exception and propagate over every import edge (`d`), because their converters embed
cross-package data, such as a skeleton for an animation, that layout providers do not record.

**What a run reports.** PLAN is silent work that can take seconds on a large dump, so it announces
itself before starting and summarises when it finishes:

```
Planning against the manifest written 2026-08-03 16:24 with 76,979 sources ...
Plan: 1,275 to add, 14,369 to update, up to 14,367 to delete, 61,335 unchanged (planned in 7.3s)
Plan: the previous run spent 00:20:06 on the sources being updated
Commit: deleted 12 orphaned output(s), manifest written in 2.1s
```

The delete count is the upper bound `ProjectOrphans` computes, since what a re-exported source
produces is unknown until it runs; the commit line reports what was actually removed. The cost is
summed from the times recorded for exactly those sources by whichever run last exported them, so
it is a record rather than a forecast, and additions contribute nothing because they have never
been exported. `--verbose` adds the per-step timings and the rule attribution:

```
  plan steps: manifest 0.8s, fingerprints 1.7s (185,003 entries), sources 0.3s (76,979 in scope), rules 4.3s
  stale by: script bytecode flag flipped 4,336 | propagated from another source 11,308
```

These lines go through `RuntimeLogging.LogSummary`, which keeps them visible under `--compact`,
where the console otherwise carries only the progress bar; they are written to standard error so
the bar keeps standard output to itself.

### EXECUTE

Unchanged from a from-scratch run except for two seams: the work list restricts which paths
`ExportProcessor` iterates, and a `SourceRecorder` is threaded through so every export records what
the next run's PLAN will need.

The work list arrives through a dedicated `incrementalWorkList` parameter on
`ExportProcessor.ProcessFiles`, not the pre-existing `typeFilteredPaths` seam the spec originally
proposed reusing. Reusing it would report every source skipped only because it is unchanged as
"type expression mismatch" under `--verbose`, which is a different situation than a genuine type
filter miss and would mislead anyone reading the log.

| File | Role |
|---|---|
| `SourceRecorder` | Collects one `SourceRecord` per source: `BeginSource`, `ObservePackage`/`AddArtifacts`/`AddMediaDependency`/`MarkExternalWwise`, `EndSource` |
| `PackageDependencyReader` | Package-level import identities from a loaded package's import map, no deserialization |
| `TypeChainWalker`, `TypeChainResult` | Walks each export's class chain to produce `t` (usmap names answered), `u` (asked but unanswered), and the layout provider set behind `p` |
| `SourceRecord` | Plain, un-interned per-source facts, handed to `ManifestBuilder` |
| `PackageModeProcessorBase`, `JsonPackageProcessor`, `AudioPackageProcessor`, `AudioExporter`, `AudioBankExporter`, `SimpleFileExporter` | Existing exporters, extended to report artifacts, failures, skip-list outcomes and Wwise media provenance to the recorder |

If a source's recorded artifacts, dependencies or type usage look wrong, the fault is in
`SourceRecorder` or in whichever exporter feeds it. If a source that should have been skipped as
unchanged was exported anyway, or vice versa, look at how `incrementalWorkList` reaches
`ExportProcessor`, not at the exporters themselves.

### COMMIT

Runs after every source in the work list has been attempted. Order matters and is fixed:

1. Export completes.
2. Orphans are computed by comparing the previous manifest's outputs against the new manifest
   `ManifestBuilder` just built, never against the plan, because only once every re-exported source
   has actually run is it known what it produced.
3. Orphans are deleted, then any directory those deletions left empty is pruned up to the output
   root.
4. The manifest is written last, temp file then rename.

| File | Role |
|---|---|
| `ManifestBuilder` | Interns every string and set exactly once; re-interns carried-forward entries; assembles the final `ExportManifest` |
| `OrphanCleanup` | Finds and deletes outputs no source in the new manifest claims, then prunes emptied directories |
| `ExportManifestStore.Save` | Atomic write: temp file, then rename |
| `ExportManifest`, `ManifestSource`, `ManifestUsmapBlock`, `BytecodeState`, `SourceStatus` | The manifest's own data model |

If the manifest on disk looks internally inconsistent, an id pointing at the wrong string, an
entry missing a table row it should have, the fault is almost always in `ManifestBuilder`'s
interning or carry-forward logic, not in `ExportPlanner`.

## The manifest format

One file, `.uas-manifest.json`, at the output directory root. It is always a complete description
of the dump, never a delta. It is written indented, so the global block at the top can be read when
reviewing how a run was configured, except for the source entries: `ManifestSourceConverter` keeps
each of those on a single line, which stays greppable and avoids burying that block under one
multi-line entry per source.

```json
{
  "schema": 2,
  "mode": "textures",
  "game": "GAME_UE5_1",
  "tool": [{ "uas": "0.2.1.0+1a2a277", "cue4parse": "1.2.2+a098f0b6" }],
  "skipTypes": ["UTexture2D"],
  "scriptBytecode": false,
  "containers": ["pakchunk0-Windows.pak"],

  "usmap": {
    "types": { "0": "9f2a..." },
    "enums": { "0": "77ab..." }
  },

  "paths":         ["Game/Content/Foo/T_Wall.uasset", "Game/Content/Foo/T_Wall.uexp",
                     "Game/Content/Foo/M_Wall.uasset"],
  "outputs":       ["Game\\Content\\Foo\\T_Wall.png"],
  "ueTypes":       ["Texture2D", "Object", "TexturePlatformData"],
  "ueEnums":       ["EPixelFormat"],
  "clrTypes":      ["UTexture2D"],

  "typeSets":      [[0, 1, 2]],
  "clrTypeSets":   [[0]],
  "clrTypeChains": { "0": [0] },

  "fingerprints":  { "0": "3q2+7w==", "1": "kZ8xAA==" },

  "sources": {
    "0": {"c":[0,1],"d":[],"p":[],"o":[0],"t":0,"u":null,"x":0,"e":false,"b":"false","s":"ok","ms":18.375}
  }
}
```

### Global block

| Field | Purpose |
|---|---|
| `schema` | Format version. Checked in `ExportManifestStore.TryLoad`: a manifest whose schema does not match the current build's is an error in the same shape as an unparseable one, naming both values and pointing at `--rebuild`. Bumped only when defaulting a field could change what a plan decides, which is why `p` required a bump and `ms` did not: an absent `p` leaves the propagation graph without edges and silently under-invalidates, while an absent `ms` only leaves a cost unknown until that source is next exported |
| `mode` | Mismatch is a gate error: a different mode produces entirely different outputs |
| `game` | Mismatch is a gate error: changes parsing in ways no source fingerprint would catch |
| `tool` | Every `(uas, cue4parse)` pair, each carrying a git sha, that has contributed output currently on disk, most recently used last. Gated: see the sticky-parts entry below |
| `skipTypes` | The resolved skip set, stored so PLAN can diff it against the current run's rather than gate on it |
| `scriptBytecode` | The *effective* value, `mode == json && flag`, not the raw flag; see below |
| `containers` | Guards a wrong AES key or a missing pak silently unmounting containers, which would otherwise make every source look removed and orphan deletion would wipe the dump |

Both halves of a `tool` pair carry a git sha, stamped into assembly metadata by an MSBuild target
at build time. Neither uas nor CUE4Parse exposed a git-derived identity before this feature: a
build number alone would not change across a submodule bump, which is exactly the kind of change
this gate exists to catch.

### Interning tables

| Table | Contents |
|---|---|
| `paths` | Container-relative source, constituent and dependency paths |
| `outputs` | Output-dir-relative output paths |
| `ueTypes` / `ueEnums` | UE type and enum names |
| `clrTypes` | CUE4Parse C# class names, a distinct vocabulary from UE names: `Texture2D` and `UTexture2D` are not the same key, and the skip predicate must never be evaluated against the wrong one |
| `typeSets` / `clrTypeSets` | Distinct sets of ids, deduplicated so packages sharing the same type usage share one row |
| `clrTypeChains` | `clrTypes` id to the ids of that type's own name plus every base type name, nearest first, stopping at (not including) `object` |

`ueEnums` holds only the names fingerprinted directly from the usmap's own enum table. An enum
name the class chain walk consults reaches the manifest through `ueTypes` instead, alongside
struct and class names, because the walk's own result does not distinguish an enum reference from
a struct reference; `ueEnums` exists solely to key the usmap block's per-enum fingerprints, and
looks unused for anything else because it is.

### The usmap block

A usmap is reduced to one semantic fingerprint per type and per enum, not a fingerprint of the
file's bytes:

- a type's fingerprint covers its own name, its supertype's name, and every one of its own
  properties in index order, each as (index, property name, type descriptor); the supertype's own
  members are never walked into, since each level of a chain is fingerprinted independently anyway
- an enum's fingerprint covers its own name and every (value, member name) pair, sorted

Semantic rather than byte-based fingerprinting means a regenerated usmap for an unchanged game compares
equal even if the dumping tool reordered its internal name table; a byte-for-byte comparison would
falsely invalidate on every regeneration regardless of whether anything the game actually declares
changed. No file path is stored: an empty `types` map means no usmap was supplied for this run,
which correctly invalidates every source that ever depended on usmap-known types, since every
recorded type name it used has effectively disappeared.

`fingerprints` is keyed by path id, not by source: a payload file is not a source in every mode
but still needs a hash so its change can invalidate whatever reads it, and a dependency identity
(a package name, or a `packageid:` token) is interned into `paths` and keyed the same way so its
own fingerprint entry can be looked up identically to any container path. An entry in `paths` with
no corresponding row in `fingerprints` means an import that did not resolve: nothing was ever
found to hash.

### Per-source fields

| Field | Meaning |
|---|---|
| `c` | Constituent path ids, including the source's own file, so staleness needs no special case for the header |
| `d` | Dependency identities: package imports (by name or `packageid:` token) plus Wwise media container paths, merged, recorded whether or not they resolved |
| `p` | Layout provider path ids: the packages whose live structs supplied class chains, struct layouts, or enum names for this package's exports, recorded as extensionless package names and resolved back to sources at plan time through a stem index. The propagation edges for every mode except `models` and `animations` |
| `o` | Output ids from what the export actually produced. The only way to delete orphans in modes where output names come from export objects rather than the source path |
| `t` | `typeSets` id: usmap names the package's deserialization consulted and the usmap answered |
| `u` | `typeSets` id: consulted names the usmap could not answer |
| `x` | `clrTypeSets` id: CLR export type names for the skip predicate. `json` mode only |
| `e` | Set when any extracted sound came from an unfingerprinted external file |
| `b` | `true`, `false` or `unknown`; see the sticky-parts entry below |
| `s` | `ok`, `failed`, or `skipped-by-skip-list` |
| `ms` | Milliseconds this source took to export, measured across the work between `BeginSource` and `EndSource`. Carried forward unchanged for a source this run did not re-export, so a plan can total the cost of every source it covers, not only the ones it re-exported |

Sources are keyed differently by mode: package modes key by the package file only, with payloads
appearing solely in `c`; `simple` keys everything else that is a source in that mode; `raw` gives
every file, payloads included, its own entry.

## The sticky parts

### Why are fingerprints read rather than computed, and why is the pak hash offset computed from the pak version instead of being a constant?

Reading is a fixed, small cost regardless of a file's size: the packer already computed a strong
hash when the container was built and stored it inline. Hashing content instead would mean
reading and hashing every byte of every asset on every run just to find out that almost nothing
changed, which is the exact cost incremental export exists to avoid.

The offset is not constant because `FPakEntry`'s own inline header is not a fixed shape across pak
versions: the compression-method field is one byte on the modern name-based-compression version
and four bytes (a legacy `int32` enum) on every other version, and a timestamp field is present
only below the version that dropped it. A single flat offset is correct for exactly one shape and
silently wrong, not absent, for the others: it would read a fingerprint-shaped value from the
wrong bytes and quietly skip real work forever. The offset is therefore derived from the entry's
own `Version` and `IsSubVersion`, mirroring the same layout logic `FPakEntry`'s parser already
uses, and every read is cross-checked against the entry's own recorded sizes before the bytes at
that offset are trusted as a hash.

### Why does the `.utoc` have to be re-read, when the provider has already parsed it?

The provider mounts IoStore containers for asset extraction with `ReadDirectoryIndex` only, so the
live `TocResource`'s chunk metadata is null; loading per-chunk hashes is skipped because ordinary
export does not need them. PLAN does, so it re-reads the raw `.utoc` bytes with `ReadTocMeta`, the
same option `IoStoreReader` itself would use if it needed that data.

### Why are package dependencies recorded as names rather than paths, and why is "absent on both sides" not a change?

An import that never resolves has no path to record at all, so a path-shaped identity cannot
represent it. Worse, a path token would change the moment the import did resolve, which would
destroy exactly the signal this field exists to catch: a package whose own bytes are unchanged but
whose class chain now reaches further because a patch added what it was missing. A name, or for
IoStore imports a `packageid:` token built from the same hash CUE4Parse itself uses to resolve an
import by id, stays the same identity whether or not it currently resolves.

That is also why absent on both sides of a comparison counts as unchanged rather than changed. An
unresolved import is common and often permanent (optional content, a plugin not installed). If
"absent" counted as a change, a package with such an import would be marked stale, and therefore
re-exported, on every single run forever, which defeats incremental export for exactly the sources
it should help most.

### What are `t` and `u`, and why are live chain levels not recorded?

`t` is every usmap name the package's deserialization actually asked for and got an answer to.
`u` is the names it asked for that the usmap could not answer. Both come out of the same walk,
which records two kinds of name: where each export's live class chain ended, and property
references that did not resolve to a live loaded object.

A live chain level, a class loaded from a real package, takes its layout from that package's own
property data: CUE4Parse builds the unversioned schema from the live struct and never reads the
usmap entry that may also exist under the same name. Recording live names would couple
every blueprint-heavy package to usmap entries that cannot affect its output, and separate dumps
of one game version differ almost exclusively in exactly those entries, so a mere regeneration would
invalidate a large share of the export for zero byte changes. Changes that genuinely arrive
through a live level are covered by the walk recording that level's owning package as a layout
provider (`p`), which is what staleness propagates over.

The usmap is consulted in exactly two places, and both are recorded. Where the live walk ends,
layout sourcing hands over to the mappings: a script class takes its own layout and everything
above it from the usmap, and `ConstructObject`'s chain repair reads the ending level's entry by
name whenever the chain cannot continue. And a struct or enum property reference is consulted by
name whenever it did not resolve to a live loaded object, mirroring the live-first preference in
`PropertyType` and `IndexToEnum`. Plan-time closure expansion covers everything reachable from a
recorded name through the usmap's own supertype and reference graph, which is why one recorded
name per chain ending is enough.

A consulted name the usmap could not answer goes to `u` rather than being dropped: deserialization
was truncated, or the repair had nothing to read, so if a later usmap gains that name the output
changes and the package must re-export. The planner therefore checks `u` against both the type and
the enum tables of the current usmap.

### Why does staleness propagate over layout providers rather than import edges?

A game's import graph is hub-dominated: shared widgets, base classes and common modules are
imported, directly or transitively, by most of the content, and mutual imports weld the gameplay
region into one densely connected component. Propagating over import edges therefore converges on
roughly the same large basin regardless of how small the patch is; the re-export count measures
the graph's shape, not the change. Almost all of that transitive work is redundant, because the
direct dependency rule already covers the first hop of every content change, including the subtle
one: a rendered reference carries the target's export index, which any content change can shift.

Beyond that first hop, the edges that can still carry an output change are the layout ones:
a package renders indices only for its own imports, so a change two or more hops away reaches its
output only when the deserialization embedded a layout from along the way, an instanced class, an
inherited widget tree, a user-defined struct or enum. Those provider packages are exactly what
the type chain walk already visits, so recording them costs nothing at export time, and
propagation over them keeps the correctness guarantee while no longer paying for the basin. The
conversion modes keep import-edge propagation because their converters embed cross-package data
the walk never sees. Narrowing the direct rule itself the same way is not sound; the attempt was
built behind a flag and falsified by the end-to-end protocol before it shipped.

### Why does `b` have three states, and why is `false` the only one safe to skip on a flag flip?

With the bytecode flag on, an export is always directly observed: `true` if any struct's
`ScriptBytecode` is non-empty, `false` otherwise. With the flag off, CUE4Parse reads
`serializedScriptSize` into a local and discards it, leaving no trace, so a re-exported source
records `unknown` and a carried-forward source keeps whatever it already had.

`false` is the only state guaranteed unaffected by a flag flip in either direction, because it was
reached by direct observation under the flag being on: there was no bytecode to begin with, so
turning the flag off changes nothing about that fact, and having already been observed with the
flag on means the value cannot suddenly become stale by turning it on again. `true` and `unknown`
both have to be treated as possibly wrong on a flip, either because real bytecode would newly
appear or disappear from output, or because the value was never actually observed.

### Why is the skip list diffed rather than gated, and why do base-type chains live in a manifest-level table?

Diffing means a change to the skip list invalidates only the sources whose predicate result
actually flips, not everything. Gating would force a full rebuild on every list edit, which is
disproportionate: the predicate is cheap to evaluate once the export's types are known, and those
types are already recorded. The `--filter` scope option gets no such treatment because it is
evaluable instantly against a path with no package load at all; the skip predicate needs a
package's export types, which come only from loading it or from what was already recorded, so
keeping a `skipped-by-skip-list` entry with empty outputs is what lets the next run answer the
question without reloading and reconstructing the package again.

The predicate matches base types as well as an export's own type, so evaluating it later from a
bare leaf class name is impossible without knowing that leaf's ancestry. A chain is a property of a
CLR type, not of a package, and many packages share the same export types, so recording each
distinct chain once in a manifest-level table and referencing it by leaf name is far cheaper than
duplicating the chain inside every source that happens to use that type.

### Why does carry-forward re-intern ids instead of copying them?

The old manifest's interning tables and the new one being built are never guaranteed to agree.
Paths and types come and go between runs, so the same numeric id in each file can point at a
completely different string. Copying an id verbatim from the old manifest into the new one would
silently attach it to whatever unrelated entry now occupies that slot: not a crash, a quiet
corruption that only shows up later as a wrong staleness decision. Carry-forward instead resolves
each old id back to its string through the old tables, interns that string into the new tables,
and writes the resulting new id, which is the only operation that is correct regardless of how the
two tables' shapes differ.

`ManifestBuilder.Build` has its own version of this hazard: interning chain names during
`AddClrTypeChains` can add entries to the `clrTypes` table, so that interning has to happen, and be
captured into a local, before `clrTypes` is read for the final `ExportManifest`. `Build` is written
as sequential statements rather than relying on object-initializer evaluation order for exactly
this reason.

Carry-forward re-interns `t`, `u`, `d` and `o` but never recomputes them, and that is safe only
because none of them is re-derived without also being re-verified: every one is a function of this
source's own bytes and its transitively imported packages' bytes, both already confirmed unchanged
by the direct staleness rules before an entry is eligible to carry forward, plus the usmap, which
the changed-type closure and the `u`-presence rule already cover for `t` and `u` respectively. A
source only reaches carry-forward once every input its recorded fields could depend on has already
been checked, so re-interning its old answer is not trusting stale data, it is copying an answer
already known to still be correct.

### Why is the manifest written last, and what can a crash leave behind?

Writing the manifest only after every deletion and export has completed means a run that dies
mid-way leaves the manifest describing the last known-good state, so the next run replans from a
real baseline instead of one that claims work was done when it was not.

A crash between some exports completing and the manifest being written can leave freshly written
output files with no entry in any manifest. The ordinary case self-heals: the next run finds those
sources missing or stale and re-exports them, overwriting the same output paths. The residue that
does not self-heal is a source whose output name changed as part of the same crashed run: the old
name from before the crash is not in the old manifest's claimed set forever (it may already have
been superseded), and the new name is not in any manifest either, so it is neither cleaned up as an
orphan nor recognised as current. This is accepted: it can only happen inside the narrow window of
a crash after export but before the write, and the alternative, writing the manifest earlier, would
make the "last known good state" claim false.

### Why does the tool gate compare set membership rather than equality, and why is acceptance sticky?

`tool` is an array of every version pair that has contributed output currently on disk, not a
single current value, so the gate asks whether the running pair is anywhere in that array rather
than whether it equals the most recent entry. That is what makes acceptance durable: the first run
against a new pair needs `--accept-tool-version` to proceed, but every later run on that same pair
finds it already present and proceeds silently, so the override is typed once per upgrade instead
of on every invocation. A downgrade to a pair already in the array is silent for the same reason,
which is correct: the outputs already on disk from that pair are no worse for it. Nothing separate
records that an override ever happened, because the array already says so on its own.

### Why does the gate compare an export-behaviour number rather than the uas version?

The gate exists to answer whether outputs already on disk still match what this build would
produce. A release that changes nothing about exporting produces identical bytes, so a release
version fails the gate on every upgrade, costs an `--accept-tool-version`, and leaves the dump
marked as a mixture, all for output that cannot differ. `ExportCompatibility.Version` is bumped by
hand only when a change can alter exported bytes, so an upgrade that does not touch exporting
compares equal and passes silently.

That moves the failure mode rather than removing it. A spurious gate is safe; forgetting to bump
the number carries stale outputs forward with no warning at all, and the only way to notice is to
diff against a full rebuild. Deriving the number instead, by hashing the exporter sources, was
rejected: a comment edit or a rename would invalidate every dump on disk, which is the same false
invalidation in a less predictable form. The obligation is therefore documented as a release gate
in `CLAUDE.md` rather than enforced mechanically.

The CUE4Parse half is a bare commit hash. CUE4Parse is pinned as a submodule by commit, so the hash
is the whole of its identity; the version it declares moves on its own schedule and says nothing
about which code is compiled in. A version recorded here would also be unreliable: a command-line
`-p:Version` is a global MSBuild property that reaches every project in the graph, so a release
build stamps CUE4Parse's assembly with the uas version.

### Why is `scriptBytecode` recorded as the effective value rather than the flag?

The flag only affects output in `json` mode; in every other mode CUE4Parse's bytecode extraction
has no bearing on what gets written. Recording the raw flag value would mean flipping
`--script-bytecode` while exporting, say, textures would trip the "flag flipped and `b` is not
`false`" rule for every textures source, invalidating work that could not possibly have changed.
Recording `mode == json && flag`, the value that actually governs whether bytecode is ever read or
written, means a flip outside `json` mode is invisible to this rule because it genuinely cannot
affect output.

### Additional invariants without an automated guard

A few facts do not fit the question-and-answer list above but matter just as much, because nothing
in the test suite fails automatically if they regress.

**The source set must be built from resolved provider paths, never from the provider's raw
per-container entries. PLAN holds to this; EXECUTE does not, and the gap is closed one step later
instead.** A container dictionary yields an entry once per container that mounts a path, so a file
present in both a base and a patch container appears twice, patch entry first, and the provider's
own indexer resolves to whichever entry wins on lookup, which is not necessarily the one a naive
fold over "every entry" would keep. PLAN's fingerprinting and source-set building both go through
the same resolving indexer, so the fingerprint recorded is always the fingerprint of the file the
provider itself resolves for that path, not an artifact of container enumeration order. Building
either one from raw per-container entries instead would silently record a stale or a
duplicate-keyed entry with no error at all.

`ExportProcessor.ProcessFiles` does not share this property: it iterates `provider.Files.Values`
directly, the same raw per-container sequence PLAN avoids, so a shadowed path is still opened and
closed twice during EXECUTE, once per mounting container, producing two `SourceRecord`s for one
path. Changing what `ExportProcessor` iterates is out of this feature's scope, so `IncrementalRunner`
corrects the result afterward instead: it keeps only the first of the two records, which is the one
from the same container the provider's own indexer (and therefore PLAN's fingerprint) resolves to,
so the manifest's recorded metadata and its recorded fingerprint end up describing the same
underlying file rather than mismatched copies. The residual gap is whatever only the other,
discarded copy would have contributed, most notably a dependency present in that copy's import map
but not the kept one's; see Known limitations.

**A dependency identity's own fingerprint must be recorded under the identity string itself, not
only under whatever path it currently resolves to.** The staleness rule for a dependency compares
the fingerprint recorded against an identity when the manifest was written to the fingerprint that
identity resolves to now. If nothing ever records a fingerprint keyed by the identity string, that
comparison always sees "recorded: absent, current: resolved" and treats every dependency as
changed, which marks nearly every source with any package dependency stale on every run: the
feature would appear to work while saving almost nothing, because both trees still export
correctly and nothing but full re-processing looks wrong. Every dependency identity encountered
during a run is therefore resolved to its current path and that path's fingerprint is recorded a
second time, under the identity, alongside the ordinary per-path entry.

**Usmap type and enum names are compared ordinally, and the closure walk's visited set and the
changed-name set are both case-insensitive.** CUE4Parse's own usmap and jmap parsers build the type
map with a case-insensitive comparer, so a supertype or a referenced-type name can differ in case
from the key it is ultimately looked up against. Comparing case-sensitively anywhere on that path
can silently fail to recognise that a changed type is the same type a chain walk consulted, which
under-invalidates: a real change is missed rather than merely reported oddly. Enum references have
no resolution step to canonicalise them through at all, so the comparer used when checking set
membership is the only place either kind of mismatch is ever caught. `TypeChainWalker.Classify`'s
own sets use plain ordinal comparison deliberately, for the opposite reason: those names are
engine-authored identities being deduplicated by exact identity, not human text where a
locale-aware comparison would be wanted, and a culture-sensitive default comparer can treat two
ordinally distinct names as equal and silently drop one.

**`--rebuild` must bypass a manifest load error, not just a valid-but-mismatched manifest.** The
gate's ordinary error path exists for a manifest that parses but does not match this run; a
manifest that fails to parse at all is reported with a message that itself tells the user to pass
`--rebuild`. If the run loop treated a load failure as fatal before ever consulting
`options.Rebuild`, that advice would be a dead end: the documented escape hatch would not work for
the one case its own error text points at.

Recovering this way still has a cost: `--rebuild` over a manifest that failed to load runs with no
previous manifest at all, so `OrphanCleanup.FindOrphans` has nothing to diff against and returns no
orphans. Every output the corrupt manifest once claimed, but that the rebuild's own scope no longer
produces, is left on disk with no entry in the manifest the rebuild writes. It is not deleted, and
because the manifest that once tracked it can no longer be read, no later run can identify it as an
orphan either; it becomes an untracked file for good, in the same sense as the untracked-files
limitation below, except the tool produced it rather than a user.

**Every leaf name recorded in a source's `x` must have a corresponding entry in the manifest-level
chain table, and nothing enforces this automatically.** `SourceRecorder`'s CLR type collection and
its chain collection are built from the same grouping so a leaf can never be produced without a
chain in the code as written, but no runtime check anywhere rejects a manifest where the two have
drifted apart. If a leaf name ever loses its chain entry, the skip predicate does not error or
invalidate that source, it silently falls back to treating the bare leaf name as its own one-name
chain, which changes what the predicate can match without failing loudly. A change to how `x` or
the chain table is populated should be checked against this invariant by hand.

**A source with any export failure is recorded as `failed`, even when other exports from the same
package succeeded.** `SourceStatus` has no partial state. Recording such a source as `ok` because
it still produced some outputs was an actual defect during implementation: a manifest that claims
`ok` with a shorter output list than a healthy run would hide a real failure indefinitely, since
`ExportPlanner` never reads `s` and the output-missing staleness rule only fires when a
previously-tracked output later goes missing, not when one was never produced in the first place.
Skip-list-driven emptiness is checked first and recorded as `skipped-by-skip-list` instead, because
a skipped package never attempts an export and so never has a failure to report.

**`--dry-run` projects orphans from the manifest actually on disk, not from the plan's baseline.**
`ExportPlanner.Plan` sets a plan's baseline to null whenever `--rebuild` is set, regardless of
whether a manifest exists, because a rebuild's carry-forward set is genuinely empty. Projecting
`--dry-run`'s "would delete up to N" figure from that same baseline would therefore report zero
deletions for `--rebuild --dry-run` against an existing dump, immediately before a real
`--rebuild` run deletes everything that manifest tracked. The projection is computed from the
manifest loaded from disk instead, so the two flags combined report an honest, if intentionally
over-counted, upper bound rather than a misleading zero.

## Options

### Classification

Every option that can change written output is handled by exactly one of three mechanisms; one
that cannot change output is not recorded at all. There is no catch-all hash over "everything
else."

| Mechanism | Where it acts | Recorded as | Effect of a change |
|---|---|---|---|
| Scope | PLAN rebuilds S | nothing | Implicit: new sources are novel, dropped sources are orphaned |
| Precise | PLAN diffs the stored value | plain value | Invalidates only the sources whose behaviour actually flips |
| Gate | PLAN's gate | plain value | Error and stop; `--rebuild` proceeds |
| Not recorded | nowhere | nothing | None; the option cannot change a written output |

An option that can change output and is left unclassified is silently ignored by PLAN, and a run
will skip work it should have redone. There is no fallback that catches such an omission; adding
an option to the CLI means adding a row here.

| Option | Mechanism |
|---|---|
| export mode subcommand | Gate, `mode` |
| `--game` | Gate, `game` |
| `--paks`, `--aes`, `--aes-file` | Gate, indirectly: a wrong or missing key unmounts containers and the `containers` check fires. The key itself is never recorded |
| `--output` | Identity; it is where the manifest lives |
| `--usmap` | Not recorded. The file's content is fingerprinted semantically instead, so regenerating it to a different path is not a change |
| `--filter`, `--expression`, `--types` | Scope |
| `--skip-types`, `--skip-types-file`, `--no-skip-types` | Precise, resolved into `skipTypes` |
| `--script-bytecode` | Precise, the effective `scriptBytecode` value |
| `--rebuild`, `--dry-run` | Control the run itself; nothing to record |
| `--verbose`, `--compact`, `--mark-usmap`, `--log`, `--log-append`, `--no-log`, `--log-libs`, `--log-counter` | Not recorded. Affect logging, progress display and run statistics only, never a written output |
| `--format`, `--file` | `list` modes only, which write no dump |
| `--accept-tool-version` | Narrow escape hatch past the tool gate alone; invalidates nothing by itself |

`--expression`/`--types` is scope rather than needing the skip list's diff-based treatment because
deriving the path set it produces needs no package load, exactly like `--filter`, so it is as cheap
to fold into S directly as any other scope option.

### Scope versus the skip list

`--filter` and `--expression`/`--types` define S itself: a source outside S has no surviving entry,
nothing claims its outputs, and they are deleted as orphans. Its fingerprint is still recorded, so
an in-scope source that depends on it still sees the change.

The skip list produces a boolean per package too, but is not treated as scope, because `--filter`
is evaluable against a bare path while the skip predicate needs a package's export types. There is
also an asymmetry in what exclusion costs: a filtered-out source has to be re-exported regardless
of whether its entry survives, since returning to scope means producing its outputs again either
way, but a skip-listed package never had outputs, so keeping its entry with empty `o` and status
`skipped-by-skip-list` avoids reloading and reconstructing it purely to re-derive an answer already
on record. The dump on disk is identical either way; the manifest just carries the extra rows that
let the next run skip cheaply.

## Known limitations

1. External Wwise media that comes from an unfingerprinted file rather than the mounted VFS is not
   fingerprinted. Any source touching one is unconditionally re-exported every run. This also
   applies whenever CUE4Parse's own reader falls back to a non-partial-read path for a game that
   does not support partial archive reads: genuine Wwise media in that situation records neither a
   dependency nor the external-media flag, which is correct given what is observable, but worth
   knowing if invalidation for such a source looks unexpectedly total or absent.
2. FMOD and CriWare extraction returns bare bytes with no source provenance, so per-file
   dependencies cannot be captured for them the way Wwise's file-backed reads allow.
3. Wwise bank discovery matches by an id scan, so a newly added bank can change which file answers
   a lookup without any recorded dependency changing.
4. usmap regeneration can legitimately invalidate work that a game change did not cause: dump
   tooling for the mapping format is not guaranteed deterministic between two runs against an
   identical build. The mitigation is workflow, regenerating only when the game itself changed, not
   a code fix.
5. Untracked files are never deleted, so a dump can contain files a from-scratch run would not have
   produced.
6. A crash between some exports completing and the manifest being written can leave a stray output
   behind if the crashed run also changed that source's output name; see the sticky-parts entry
   above.
7. Output content is not verified, only existence. An output modified outside the tool goes
   unnoticed.
8. Transient export failures are not retried; a failed source is invalidated by the same rules as
   any other, not automatically re-attempted.
9. A dump carried across an accepted tool version change is no longer bit-exact. This is explicit
    user consent, reported on every subsequent run, and only `--rebuild` closes it.
10. For an IoStore package present in more than one mounted container (a base container and a patch
    that also contains it), which container's import list is read to build `d` is not guaranteed to
    be the patch's: the lookup CUE4Parse exposes for this is its own designated fallback path and
    iterates mounted containers in no defined order. A stale dependency list can therefore be
    recorded for such a package, which under-invalidates on a patched IoStore game specifically.
    There is no clean fix available through CUE4Parse's current public surface: it does not expose
    which container a package's headers actually came from, and the only alternative that does
    would force-load every declared import just to enumerate them, which defeats the point of
    reading dependency identities without deserializing the packages they point at.
12. For any path present in more than one mounted container, pak or IoStore, `ExportProcessor`
    still opens and closes a source record once per mounting container, because it iterates the
    provider's raw per-container entries rather than the resolved path set PLAN itself uses; see the
    invariant entry above. `IncrementalRunner` keeps only the record from the container the provider
    resolves the path to, matching the fingerprint recorded for it, so recorded metadata and the
    recorded fingerprint always describe the same copy. What that discards is whatever only the
    other, unresolved copy would have contributed: a dependency present in that copy's import map
    but absent from the resolved one's goes unrecorded, and the same is true of any usmap type
    consultation or CLR export type unique to that copy. This narrows the pre-existing IoStore-only
    limitation above to a general one, and, like it, has no clean fix without changing what
    `ExportProcessor` exports, which this feature does not do.

Separately, and not something this feature fixes: CUE4Parse's own struct-lookup helper has no cycle
guard, and the usmap format is flat and cannot express two distinct, namespaced structs sharing a
name, so a valid parent-child relationship between them can collapse into an apparent
self-reference. `UsmapClosure`'s own visited set exists partly because of this; the deeper issue
belongs upstream.

## What the tests do and do not cover

Every direct staleness rule, the gate, propagation to a fixpoint, carry-forward re-interning, and
the options classification are covered by planner unit tests that need no game: they drive
`ExportPlanner`, `ManifestBuilder`, `SourceSetBuilder` and the fingerprint readers against plain
data and small on-disk fixtures.

An integration suite gated on `UAS_TEST_PAKS` and `UAS_TEST_USMAP`, with `UAS_TEST_USMAP_2`
additionally required by the regenerated-usmap case, drives `Program.Run` end to end against a
real, locally supplied game: idempotence on a second run with every timestamp preserved, recovery of a
hand-deleted output, survival of an untracked file, exact re-export from a hand-edited fingerprint,
a second usmap generated for the same game version invalidating nothing, gate errors for a wrong
mode, a truncated manifest and a mismatched schema, `--rebuild` recovering from a corrupt
manifest, invariance under varied logging options, and an isolated dependency-content-change case
that excludes the changed target from scope so only the dependency-fingerprint comparison, not
reverse-edge propagation, can explain the importer going stale. A separate script-driven end-to-end
protocol, `UnrealAssetScout/Scripts/Invoke-IncrementalE2E.ps1`, exports an old build, applies an
incremental run against a new build over that output, and compares it byte for byte against a
from-scratch export of the new build; it takes both builds' containers and usmaps as parameters
rather than assuming any fixed pair, along with the export mode, an optional path filter that
scopes every run in the protocol to the same subtree, and extra arguments passed through to each
run. A filtered run only proves the scope it covers, so it suits iterating on a planner change
while an unfiltered run remains the gate.

Explicitly not covered by any of the above:

- **IoStore fingerprinting.** The unit tests exercise the pak path; the base64 conversion of an
  IoStore chunk hash has no dedicated unit test and depends on the environment-gated integration
  suite or a real IoStore container to exercise at all.
- **External Wwise files**, since no locally available test corpus produces one.
- **FMOD and CriWare provenance**, for the same reason: no locally available test corpus exercises
  either middleware's extraction path.

## Cost model

Every phase is affordable because of what it reads and what it deliberately avoids reading, not
because of anything measured against one corpus; the reasoning below holds independent of dump
size.

- **Fingerprinting reads a hash the packer already computed rather than hashing content.** A
  container's packer already produced a strong hash for every entry at pack time; reading twenty
  stored bytes is a fixed, small cost per entry regardless of how large the entry's content is,
  where computing a fingerprint from content would scale with total dump size on every single run,
  even when almost nothing changed.
- **Package headers are read without deserializing exports.** PLAN needs each package's import map
  to build `d` and the dependency graph, and that map is available from a package's header without
  constructing the export objects it describes. Avoiding full deserialization is what keeps PLAN
  proportional to the number of packages and their headers, not to the size of everything a
  from-scratch run would eventually construct.
- **usmap closure work is memoised twice over, per distinct name and then per distinct recorded
  type set, rather than per package, and the set is already interned for storage.** Expanding a
  single type name into everything reachable from it is cached per name; deciding whether a
  recorded set intersects the changed-type set is cached again per distinct set. Many packages
  consult the same handful of type combinations, so both layers do their work once no matter how
  many packages share it, and because sets are already deduplicated for the manifest's own
  `typeSets` table, the second layer reuses a value the manifest was going to compute anyway rather
  than adding a second bookkeeping structure. The whole closure step is skipped outright whenever a
  run's usmap diff finds nothing changed at all.
- **The class-chain walk that produces `t` and `u` rides along on work `ConstructObject` already
  does.** Every export already forces the same chain of `SuperStruct` loads during ordinary package
  construction, cached as it goes; the walk that records which usmap types were consulted reuses
  those already-loaded, already-cached objects; only the property-reference scan on top of that
  walk is additional work, and it touches only property descriptors already resident once an object
  is loaded, no extra file reads.
- **IoStore chunk hashes are read once per container and cached for the run**, not once per file
  requested, so re-reading `.utoc` metadata is a per-container fixed cost rather than a per-file
  one.

## Rejected alternatives

**Excluding blueprint-generated types from the usmap diff.** Considered, because most of the churn
a usmap regeneration produces comes from compiler-generated names rather than hand-authored
content types. Rejected because there is no name heuristic that reliably classifies exactly the
compiler-generated types responsible for that noise without also risking misclassifying a real
content type on the `UScriptClass` fallback path, where a false exclusion means missing a genuine
change rather than merely doing extra work. The need later disappeared from the other side: the
walk records only names the deserialization actually consulted, so entries that exist for
blueprint classes no longer invalidate anything regardless of how the diff treats them.

**Gating on the skip list instead of diffing it.** Considered, since a gate is simpler than a diff
rule. Rejected because the skip list is exactly the kind of option whose effect is precisely
knowable: only the sources whose skip verdict flips need to change. Gating on it would force a full
rebuild on every edit to the list, which is disproportionate to what actually needs to be redone.

**Treating a chain level the usmap does not know about conservatively, rather than ignoring it.**
Considered, as the seemingly safer default when the walk cannot confirm a level's shape. Rejected
because it is unnecessary: each chain level's own referenced types are recorded independently of
whether that level itself is a usmap-known name, so nothing is actually hidden in the gap a
conservative treatment would be protecting against. Treating it conservatively would only add
invalidation with no corresponding gain in correctness.

**Recovering an IoStore import's package name by reversing its container path.** Considered, since
a name would let IoStore dependencies be recorded the same way pak dependencies are. Rejected
because the reversal does not round-trip for every mount: a project or plugin mount can fold more
than one path segment into the canonical root on the way in, and there is no general way back. A
recovered name that comes back wrong would never resolve again, silently disabling invalidation
for that dependency permanently. A `packageid:` token built from the same hash CUE4Parse itself
uses to resolve an import by id is exact by construction instead.

**Exempting the default local development version from the tool gate.** Considered, since local
builds share one nominal version and trip the gate on every rebuild during development. Rejected
because it would disable the gate specifically where output is most volatile: exactly the builds
most likely to actually change behaviour between runs. The question is now largely moot: the gate
compares an export-behaviour number rather than a version, so local rebuilds only trip it when
that number is deliberately changed.

**Folding duplicate container entries with a first-write-wins rule instead of resolving each path
through the provider's own lookup.** Considered as a simpler alternative once it was known that a
shadowed path could appear more than once. Rejected because it would only be correct by depending
on one enumeration happening to agree with a separate lookup's resolution order, an undocumented
coupling internal to the provider dependency, not a guarantee either side of it advertises.
