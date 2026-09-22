# Resume an interrupted export: design

Date: 2026-09-21. Status: agreed in brainstorming, awaiting review.

## Goal

A rerun after an export was interrupted re-exports only what the interrupted run had not finished,
instead of redoing the whole work list. A large json dump takes most of an hour, and an interruption
half way through currently costs all of that half.

The interruptions this is for are external to uas: an accidental Ctrl+C, closing the terminal
window, a power cut, and Windows hanging or bluescreening. A crash inside uas is out of scope as a
design driver: such a crash usually recurs, or is fixed by a code change before the rerun.

Success means:

- The resumed run's outputs are byte-identical to an uninterrupted run's.
- The manifest it writes has the same content as an uninterrupted run's; with records merged back
  into provider order it differs only in the `ms` timings, as any two runs already do.
- Deleting the journal, before or during a run, costs the resume and nothing else.

Not in scope: retrying sources recorded as failed, and the two related issues listed at the end.

## Invariant

The journal is never the only record of anything the dump depends on. The manifest remains the
baseline of every run, and the journal can only remove sources from the work list. A run without a
journal behaves exactly as uas does today. This is the same relationship the manifest has to a full
export: deleting the manifest forces a full run and breaks nothing else.

## Decisions

| Question | Decision |
|---|---|
| Which interruptions must a resume be correct after | All of the above, including power loss and hangs, so journaled outputs are verified before they are trusted |
| Automatic or explicit | Automatic: a matching journal is resumed and the run says so; a non-matching one is discarded with a warning |
| `--rebuild` | Resumes an interrupted `--rebuild` run with the same inputs |
| Opt-out | `--no-journal`, short `-j`: neither reads nor writes a journal |
| Format | Plain JSON Lines, not interned |
| Build compatibility | The manifest's own rule: `ExportCompatibility.Version` and the CUE4Parse commit, not the exact build |
| Journal cannot be opened or written | The run stops with an error and does not commit |
| Verification | Size and write time of every journaled output; contents only for the newest outputs, newest first |

Build compatibility follows the manifest's rule because in every interruption this is for, the
build is the same before and after. The looser rule only matters when uas changes between the
interruption and the rerun; for a release user that is `uas update`, which the release gate covers,
and for a developer between releases it is the same exposure incremental runs already have.

## The journal file

`.uas-journal.jsonl` in the output directory, next to `.uas-manifest.json`. JSON Lines: one JSON
object per line, UTF-8, separated by `\n`. A JSON encoder escapes newlines inside strings, so an
object never spans two lines and a torn tail is detectable as a line that does not parse.

The first line is the header, and every later line is an entry.

### Header

Describes the run. A later run resumes only if every field equals its own:

- journal format version
- base manifest: a SHA-256 of `.uas-manifest.json` as it was on disk when the run started, or none
- whether the run was `--rebuild`
- mode and game
- tool pair: `ExportCompatibility.Version` and the CUE4Parse commit
- usmap: a digest of the usmap fingerprints
- skip types, effective script bytecode, effective audio disambiguation
- scope: the `--filter`, `--expression` and `--types` values
- containers: the mounted container names and a digest of the source fingerprint index, so a patch
  landing between the interruption and the rerun is a mismatch

### Entry

Describes one finished source occurrence, appended at `EndSource`:

- the `SourceRecord`, exactly as `ManifestBuilder` receives it today
- the container the occurrence was read from, which tells a shadowed path's occurrences apart
- the origins of its artifacts, so the duplicate-output warning still covers resumed sources
- for each output: relative path, size, last write time (UTC) and SHA-256 of its contents
- when the source finished (UTC)

### Size

Measured by expanding existing manifests back into plain records, for a full run:

| Dump | Sources | Journal | Per source | Largest line |
|---|---|---|---|---|
| Atomic Heart json | 117,380 | 184 MB | 1.6 KB | 143 KB |
| Palworld json | 77,042 | 107 MB | 1.4 KB | 85 KB |
| Palworld textures | 77,042 | 80 MB | 1.0 KB | 186 KB |
| Remnant 2 json | 513,719 | 441 MB | 0.9 KB | 58 KB |

Plus about 80 bytes per output for its size, write time and hash. The file exists only while a run
is in flight. Interning was considered and rejected: judging by the interned manifests it would
save a factor of two to four, while a torn line could orphan every record that refers to it.
Compressed blocks could be added later without changing the semantics, if size ever matters.

## Run flow

### Before exporting

Planning is unchanged. After it:

1. Unless `-j` is given, read the journal if one exists. A missing, unreadable or zero-filled
   header, or one that does not match this run, is discarded with a warning naming the first field
   that differs. A journal left by a run that died between saving the manifest and deleting the
   journal fails the base-manifest comparison and is discarded the same way.
2. Read entries up to the first line that does not parse; everything before it is usable. For each
   occurrence (path and container) the latest entry wins, since a re-exported source appends a new
   entry. A path is a candidate only if every occurrence the provider has for it has an entry and
   the path is in the plan's work list.
3. Verify the candidates; see Verification.
4. The resumed set is the candidates that passed. Log one line, for example "Resuming: 38,120
   sources from a run interrupted at 13:48:48; contents checked back to 13:38:48; 3 re-queued".
   The work list becomes the plan's work list minus the resumed paths. A first run, which today
   passes no work list to `ExportProcessor`, passes all sources minus the resumed paths instead.

A source that was in flight when the run was interrupted has no entry, so it is exported again and
overwrites whatever part of its output reached the disk. A file only that attempt wrote, and the
redo does not, is left on disk untracked, as after any interrupted run today.

Failed and skip-listed sources resume as they were recorded. An interruption kills the process
before `EndSource`, so a failure in the journal is always genuine, and an uninterrupted run does
not retry failures either. A source with no outputs has nothing to verify and resumes directly.

### During export

5. Open the journal for writing, refusing write sharing, so a second run on the same directory
   cannot open it too. If the one read in step 1 was usable, truncate it back to the end of its
   last good line and append; otherwise start a new file with this run's header. If the journal
   cannot be opened, typically because another uas run is using the same directory, the run stops
   with an error naming the file before exporting anything.
6. At each `EndSource`, read the source's outputs back to take their size, write time and hash,
   and append the entry. The outputs were just written, so the read comes from the OS cache.
   Each entry is handed to the OS as soon as it is appended, so a killed process (Ctrl+C, a closed
   window) loses no entry. The file is forced to disk whenever two seconds have passed since the
   last forced flush, checked at each append, and when exporting ends. A hang or power loss can
   therefore lose the entries appended since the last forced flush; at worst, the ones appended
   just before a long-running source.
7. If a journal write fails, the run stops with an error, without exporting further sources and
   without committing. What was journaled before the failure stays usable, so the next run resumes
   from it. The failure must not be swallowed by the per-source exception handling in
   `ExportProcessor`.

### At commit

8. The records are the resumed records plus this run's, merged back into provider order, then
   deduplicated by path exactly as today. Merging keeps both the first-record-wins rule for
   shadowed paths and the manifest's id numbering the same as in an uninterrupted run. Artifacts
   for the duplicate-output warning are merged the same way.
9. Orphan detection, orphan deletion and the manifest save are unchanged. After the manifest is
   saved, delete the journal. With `-j`, a leftover journal is deleted here too, since it can no
   longer match once this run has committed.

## Verification

Only one failure needs the contents to be read: a file whose size and write time reached the disk
but whose data did not, which reads back as zeros. That is observed on this machine: a source file
came back as 8,340 bytes of NUL after a hang. It only affects data written shortly before the
interruption, because Windows writes cached data back continuously. The other realistic states, a
missing file, a shorter file, and previous content left in place because an overwrite never
landed, are all visible in the file's metadata.

The expected state of each output is taken from the last entry, in journal order, that lists it.
That handles outputs written by more than one source, such as shared audio media or a shadowed
path's second occurrence.

1. Metadata, for every output of every candidate: the file exists, and its size and last write
   time equal the expected ones. Write times are compared at whatever precision the output drive
   stores, which is the same on both sides.
2. Contents, newest first by the finish time of the entry the expectation came from: the SHA-256
   of the file equals the expected one. Outputs that already failed the metadata check are not
   read. Stop once both hold: the output's entry finished more than
   10 minutes before the last entry did, and the last 50 outputs checked all passed. A failure
   resets the count, so damage that reaches further back is followed until it ends. The window is
   measured from the last entry, not from the current time, so resuming a day later checks the
   same files as resuming at once.
3. An output that fails either check re-queues every candidate path with an entry listing it, and
   a re-queued path is re-exported in every occurrence. Re-exporting all occurrences in provider
   order is what reproduces an uninterrupted run, including the order in which shadowed copies
   overwrite each other.

The 10-minute window and the streak of 50 are constants, not options. Resume logs how far back the
contents were checked, so damage near the edge of the window in a real hang would be visible, and
the window can be widened.

## Options

- `--no-journal`, short `-j`: "export: Do not keep a journal of this run, and ignore any journal
  left by an interrupted one". It is not recorded in the manifest, because it cannot change a
  written output; it goes in the Incremental README's options table under "Not recorded".
- `--dry-run` (`-n`, existing) performs steps 1 to 4 read-only and includes the resume line in its
  report. It never writes, truncates or deletes the journal. With `-j` it reports no resume.
- `--rebuild` records itself in the header, so a rerun with `--rebuild` resumes an interrupted
  rebuild. `--rebuild -j` is the unconditional clean start.

## Components

New:

- `ResumeJournal`: reads and writes the file. Header, entries, torn-tail handling, truncation
  before append, the flush interval, and the stop-the-run failure on open or write.
- `ResumeVerifier`: given the entries, the candidates and a way to inspect a file, decides which
  paths resume and which are re-queued. It does not depend on CUE4Parse, so it is unit tested with
  plain fixtures and temp files, like `ExportPlanner`. The window and the streak are parameters with
  the production constants as defaults, so tests stay small.

Changed:

- `IncrementalRunner`: reads, matches and verifies the journal after planning; reduces the work
  list; opens the journal for the export; merges resumed and new records into provider order at
  commit; deletes the journal after the manifest is saved; stops the run on a journal failure.
- `SourceRecorder`: `EndSource` hands the finished record, its container and its artifacts to the
  journal.
- `ExportProcessor`: no change to its logic; it receives a reduced work list.
- Options: `--no-journal` / `-j`.

Class header comments of every touched class are updated, per `CODE_STYLE.md`.

## Testing

Keep test run time short where practical.

Unit tests, no game required:

- Journal file: round trip of header and entries; torn tails (a partial line, a zero-filled tail, a
  zero-filled file); truncation before append; the latest entry per occurrence wins.
- Header matching: one case per field, including the base-manifest hash and the rebuild flag.
- Verifier: a missing file, a shorter file, and old content with a different write time are caught
  by the metadata check; zero-filled content with the size and write time restored is caught
  inside the window and deliberately not outside it; the stop rule (window, streak, damage past
  the window followed); a shared output re-queues every writer; the all-occurrences rule. Tests
  pass a small window and streak so each needs only a handful of small files.
- Runner helpers: merging into provider order; reducing the work list; `-j` ignores the journal and
  commit deletes it; failing to open or to write the journal stops the run without committing, with
  a writer that throws injected for the write case.
- Mutation-check the key tests: break each rule on purpose and confirm a test fails.

End to end, run by hand against a real game with a narrow filter, so each run takes seconds to a
couple of minutes:

- Export once uninterrupted. Export again, kill the process part way with `taskkill /F`, which
  leaves the OS cache in the same state as Ctrl+C or closing the window, and rerun. Compare outputs
  byte for byte, and manifests with `ms` removed.
- Cover a first run with no manifest, an incremental run over an existing dump, and `--rebuild`.
- Simulate a hang's effects before resuming, since a real power cut cannot be tested safely:
  truncate, zero-fill (restoring size and write time) and delete some outputs journaled near the
  end, and confirm they are re-exported and the final dump still matches.
- Measure the cost of hashing on a sample, with and without `-j`, and extrapolate, rather than
  timing two full runs.

Whether this is a new `Invoke-ResumeE2E.ps1` or a mode of `Invoke-IncrementalE2E.ps1` is decided in
the implementation plan.

## Documentation

- A "Resume" section in `UnrealAssetScout/Incremental/README.md` with the rules above, and the
  `--no-journal` row in its options table.
- A CHANGELOG entry.
- No `ExportCompatibility` bump: outputs do not change.

## Related issues, out of scope

- Shadowed paths are exported once per container that holds them, highest read order first, and
  `PackageLoadSupport` loads the specific copy it is given. The base container's older copy is
  therefore written last and overwrites the patched output, while the manifest keeps the record
  of the patched copy. Fixing it changes output bytes and needs an `ExportCompatibility` bump.
  Resume reproduces the current behaviour and does not depend on it.
- A full disk turns every remaining source into a failure, which is committed and never retried
  (README limitation 8). Stopping the run on the first failed journal write limits the damage but
  is not a fix: the source whose output write hit the full disk may already be recorded as failed.
