# Claude Code working context

`AGENTS.md` is the durable orientation document for this repository and is imported below. Read it first. This file adds only the things Claude Code needs on top of it: the local toolchain, verified commands, the documentation authority map, and the working agreements for this repo.

@AGENTS.md

## Local toolchain

| Tool | Location / version | Notes |
|---|---|---|
| Godot editor (Mono) | `D:\Projects\Godot_v4.7.1-stable_mono_win64.exe` | GUI editor. Verified `4.7.1.stable.mono.official`. |
| Godot console (Mono) | `D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe` | Use this one for headless/CLI work and for `GODOT_BIN`. It writes to stdout; the non-console build does not. |
| .NET SDK | `10.0.302` (`global.json` pins it, `rollForward: latestFeature`) | 8.0.423 is also installed; `global.json` selects 10. The project targets `net10.0`. |
| Repository root | `D:\Projects\Fighters Through Time - V3` | Path contains spaces — always quote it. |

`GODOT_BIN` is set to the console executable by `.claude/settings.json`, so `dotnet test` picks it up without extra setup in a Claude Code session. Outside a Claude session it must be exported manually.

Both Godot executables live one directory above the repository, in `D:\Projects\`. They are not in the repo and not on `PATH`.

`D:\Projects\GodotSharp\` must stay next to the executables — it is the .NET API assembly directory that ships with the Godot Mono build, and Godot resolves it relative to the binary, not the project.

### Failure signature 1: a silently partial test run (Godot never launched)

If `GodotSharp\` goes missing or the Godot binary is moved away from it, every Godot launch aborts with:

```text
ERROR: .NET: Assemblies not found
   at: initialize (modules/mono/mono_gd/gd_mono.cpp:650)
CrashHandlerException: Program crashed with signal 11
```

The dangerous part is what `dotnet test` does next: GdUnit4 logs `Rebuilding Godot Project ends with exit code: -1073741819`, silently skips every engine-dependent test, runs only the ~20 pure-C# ones, and **still exits 0 reporting `Passed!`**. Always read the `Total:` count against the baseline below — a green exit code alone is not evidence the suite ran.

Tell: the total collapses to roughly **24** and the runner exit is `none`. Everything that ran passed, because only the pure-C# tests ran.

### Failure signature 2: a large partial total plus a negative exit code (native crash)

A *different* truncation looks like a real run that stopped partway: a **large** partial `Total:` (say 250 of 417), 0 failures among what ran, and a **negative runner exit code** — `-1073741819` (`0xC0000005`, access violation) or `-1073741795` (`0xC000001D`, illegal instruction). The GdUnit Godot child died or hung mid-suite. Unrelated suites may also start seeing `ResourceLoader.Load` return null.

This is memory corruption, not a flaky test. Never shrug it off as a flake — re-running may mask it, because the truncation point is nondeterministic, and a class of corruption that dies on one run in three will look green twice before it bites. **Bisecting by test class is misleading for this signature**: removing suites lowers the hit rate without touching the cause. Prove a subset "clean" over at least five runs before believing it.

Two causes are known, both with guards:

- **Empty Script-typed array in a resource** (Package 4 B6). `Prop = Array[ExtResource("N")]([])` where `N` is a `.cs` script corrupts the .NET heap when marshalled to its C# array export, and the process then dies at an unrelated allocation. `tests/ContentValidation/ScriptTypedEmptyArrayTests.cs` guards it; `docs/PACKAGE4_ROSTER_PLAN.md` §8 B6 has the isolation method (subtractive bisection against a repro harness).
- **Double-disposed C#-scripted `RefCounted`** (Package 5 A close). The tell is in the Godot log, not the runner output: `ERROR: FATAL: Condition "gchandle.is_released()" is true. at: mono_object_disposed_baseref` with a `GC.RunFinalizers -> GodotObject.Finalize -> Dispose` backtrace. That `CRASH_COND` compiles to `ud2` on this MinGW build, which *is* the `0xC000001D` exit. Two ways to cause it: calling `Dispose()` on a Godot `Resource` (never do this — reference counting owns them), and letting an authored `.tres` graph cycle in and out of the resource cache so its script instances are rebuilt and reaped repeatedly (load through `FTT.Core.AuthoredResources` instead). See `docs/PACKAGE5_CAMPAIGN_PLAN.md` §9.

**Always read `<user data>/Godot/app_userdata/Fighters Through Time/logs/godot.log` after this signature.** The runner output only gives you an exit code; the Godot log carries the FATAL line, the C# backtrace, and the last resources loaded. Note the log rotates on every launch — `godot.log` is the current run, the timestamped files are older ones.

### Failure signature 4: the suite hangs to the session timeout with `SceneTree.Paused` left set

`Total:` is a large partial with 0 failures, `Duration:` is only a few seconds, and the run then sits until `Aborting test run: test run timeout of 300000 milliseconds exceeded`. The Godot child is still alive and burning CPU; its log just stops.

Cause: something set `SceneTree.Paused = true` and never cleared it. GdUnit4's transport back to the .NET test host is an ordinary `Node` (`GdUnitTcpClient`, in `addons/gdUnit4/src/core/runners/GdUnitTestRunner.tscn`) with the default *Inherit* process mode, so a paused tree stops its `_process` and the harness stops receiving results. Anything that pauses — `DialogueManager` on a `PausesGameplay` sequence, `PauseMenu` — must release the pause in `_ExitTree` as well as on its normal path, and a test that pauses must restore it in `finally`.

| Signature | `Total:` | Exit code | Meaning |
|---|---|---|---|
| 1 | ~24 | `none` (0) | GdUnit could not launch Godot; only pure-C# tests ran |
| 2 | large partial | negative (`-1073741819` / `-1073741795`) | Native crash / heap corruption mid-suite |
| 4 | large partial, short `Duration:` | timeout abort, child still running | `SceneTree.Paused` leaked; GdUnit's transport node stopped pumping |
| GdUnit stdout race | large partial | `-532462766` (`0xE0434352`) | Unhandled managed exception on GdUnit's stdout-hook thread. Do not re-add the verbose engine flag to `.runsettings` |
| Cold cache | partial, 1 spurious failure | timeout abort | Import timeout artifact; see below |
| 5. Cross-worktree pipe contention | partial with **0 failures**, or a plausible `Passed! Total: ~73` in ~12 s | `100` (not negative) | Another checkout is running GdUnit at the same time; see below |
| 6. Silently dropped `[TestSuite]` | `Total:` moves by a *plausible* small delta (e.g. +1) instead of the real one | 0 (green) | A second `[TestSuite]` class shares a source file with another; see below |

### Orphaned Godot children fake signatures 1/2 in a single checkout

Confirmed 2026-08-09: two `Godot_v4.7.1-stable_mono_win64` processes left over from an earlier
GdUnit run (an aborted or crashed session leaves them alive) made the next `dotnet test` log
`Rebuilding Godot Project ends with exit code: -1073741819` and silently run only the pure-C#
subset (Total ~126 and growing as pure-C# suites are added — the signature-1 tell, at a larger
count than the historical ~24). No FATAL appears in `godot.log`. Before diagnosing heap
corruption, run `Get-Process testhost,Godot*`, kill the orphans, and re-run — the same suite
passed in full immediately afterwards. Drain **both** process names, not just `testhost`.

### Failure signature 5: cross-worktree GdUnit pipe contention (a partial total that is NOT a crash)

GdUnit4 v6.2 launches its Godot child with `--pipe-name gdunit4-FightersThroughTime`, derived from
the **assembly** name — identical in every git worktree — and Godot's
`app_userdata/Fighters Through Time` log directory is shared too. Two agents running `dotnet test`
at once cross-connect and truncate each other. Confirmed independently by six Package 6 agents.

Tells, any of which is enough:

- a partial `Total:` with **0 failures** and a **positive** exit code `100`
  (`GodotRuntimeTestRunner ends with exit code: 100`),
- `Failed to connect: Connection timeout` / `The server returned an unexpected status code`,
- `Exception All pipe instances are busy` or a *sibling worktree's absolute paths* in `godot.log`,
- `No test matches the given testcase filter` on a filter that plainly matches a built suite,
- most dangerous: a **green** `Passed! - Failed: 0, Passed: 73, Total: 73` finishing in ~12 s. That
  is the pure-C# subset only. It looks entirely plausible; the count is the only tell.

This is **not** signature 1 or 2: there is no FATAL in `godot.log`, the exit code is positive, and
the truncation point moves with the other agent's activity. Remedy: never run GdUnit concurrently
across checkouts. Drain `testhost` processes first (`Get-Process testhost`) and wait for the count to
reach zero — polling for a clear window proved far more reliable than blind retries. **Never accept a
full-suite number without comparing it to the exact expected total**; "Passed!" plus a non-trivial
count is worthless on its own.

### Failure signature 6: a second `[TestSuite]` in one file is discovered but never executed

GdUnit4 silently refuses to *run* a plain C# `[TestSuite]` that shares a source file with a
`[RequireGodotRuntime]` suite. The adapter logs `Discover: TestSuite ... with 22 TestCases found`
and then runs none of them — the run stays green and `Total:` moves by a plausible-looking `+1`
instead of `+23`. **Rule: one `[TestSuite]` class per file**, and always check `Total:` against the
exact expected delta, not against "bigger than before".

### Test-authoring footgun: `OverrideFailureMessage("")` throws on the *passing* path

`AssertThat(errors.Count).OverrideFailureMessage(string.Join("; ", errors)).IsEqual(0)` — the natural
way to surface a validator's messages — throws
`ArgumentException("The value cannot be an empty string. (Parameter 'message')")` precisely when the
content is correct and the error list is empty, because GdUnit4 evaluates the override eagerly. Use a
non-empty literal prefix, or the repository idiom
`if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");`. The resulting failure
reads like a content bug, which is why it has cost several agents a run.

A third signature — a headless scene smoke run exiting `-1073741819` inside
`Godot.Collections.Array.Finalize()` **after** the scene finished with no script errors — was a
finalizer/teardown race fixed on 2026-08-07: `GameManager._ExitTree` now drains the finalizer queue at
quit and gameplay call sites dispose engine-returned Godot collections deterministically
(`GodotCollectionExtensions.AsDisposable`). If that stack ever reappears, see
`docs/PACKAGE4_ROSTER_PLAN.md` §8 C1 item 10 for the diagnosis and fix history before bisecting —
and note it only ever reproduced in the repository checkout, never in a git worktree.

## Verified commands

Build (verified: succeeds with 1 pre-existing vendored warning — `CS8632` in `addons/gdunit4/src/dotnet/GdUnit4CSharpApi.cs`, not caused by your change. The old `CS9057` analyzer-version warning disappeared with the move to SDK 10):

```bash
dotnet build FightersThroughTime.csproj --nologo
```

Full headless test suite (GdUnit4 spawns Godot itself; `.runsettings` forces serial headless execution). Verified 2026-08-10 on `net10.0` (combat-feel round: silent placeholder audio, attack-on-the-move, universal-dash removal, enemy stand-off): **1307 passed, 0 failed, Total 1307**, in about 46 seconds after a warm build. Read the `Total:` count in the summary, not just the exit code — see the failure signatures above:

```bash
dotnet test FightersThroughTime.csproj --settings .runsettings
```

**Cold-cache gotcha.** After `.godot/mono/temp/` is cleared (or on a fresh clone), the first `dotnet test` can exhaust the 300 s `TestSessionTimeout` in `.runsettings` while Godot re-imports, then abort with `Aborting test run: test run timeout of 300000 milliseconds exceeded` and report a **spurious failure for whichever test was mid-flight** — a partial total like `Failed: 1, Passed: 81, Total: 82`. That is a timeout artifact, not a real regression. Re-run once the cache is warm before investigating, and confirm by running the named test class in isolation with `--filter`.

Single test class or method:

```bash
dotnet test FightersThroughTime.csproj --settings .runsettings --filter "FullyQualifiedName~FighterStageGeometry"
```

Headless import / project-load check — the fastest way to catch a broken `.tscn`, `.tres`, or `res://` path:

```bash
"D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "D:\Projects\Fighters Through Time - V3" --quit
```

Run a scene headless for a fixed number of frames (smoke check without a window):

```bash
"D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "D:\Projects\Fighters Through Time - V3" res://scenes/arenas/TestArena.tscn --quit-after 300
```

Open the editor for manual verification (only when the user asks — it takes over the display):

```bash
"D:\Projects\Godot_v4.7.1-stable_mono_win64.exe" --path "D:\Projects\Fighters Through Time - V3" --editor
```

Useful scene targets: `res://scenes/menus/MainMenu.tscn` (main scene), `res://scenes/arenas/TestArena.tscn` (Fighter sandbox), `res://scenes/campaign/HubWorld.tscn` and `res://scenes/campaign/Level_NN_*.tscn` (all sixteen campaign levels are authored), `res://scenes/fighter/FighterStage_*.tscn` (all ten production Fighter stages are authored), `res://scenes/diagnostics/PerformanceBaselineRunner.tscn` (performance baseline runner).

### After editing `localization/en.csv`, run `--import`, not `--quit`

`--headless --quit` performs the project-load check but does **not** reimport a changed
`en.csv` into the compiled `localization/en.en.translation` — only `--headless --import` (or the
editor) does. `Node.Tr()` reads the compiled resource, so an agent who adds keys, runs the import
*check*, and commits ships a stale translation: every new key renders as its raw key at runtime
while the CSV looks perfectly correct in review. Run `--import` after editing `en.csv` and commit
the regenerated `en.en.translation` alongside it. `tests/ContentValidation/CampaignLocalizationTests.cs`
fails on exactly this drift for the campaign key families, and the roster suites do the same for
enemy/boss display names.

```bash
"D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "D:\Projects\Fighters Through Time - V3" --import
```

## Shell notes

- The default shell is Windows PowerShell 5.1. It has no `&&` or `||` chaining — use `A; if ($?) { B }`. The Bash tool is also available and takes POSIX syntax.
- Every path into this repo needs quoting because of the spaces in `Fighters Through Time - V3`.
- Set the test environment in PowerShell with `$env:GODOT_BIN = "..."`, not `export`.
- A cold `dotnet test` run has to import the whole Godot project first; allow several minutes before treating it as hung.

## Documentation authority map

`docs/` now holds only current Godot-era material; the legacy Unity archive was removed on 2026-08-06 (see below). Numbers still come from resources, not prose, so check the tier before quoting any document.

**Authoritative — implement against these:**

| Path | Role |
|---|---|
| `design-godot.md` (repository root) | The full product and technical specification. This is the canonical design document. |
| `AGENTS.md` | Durable architecture/context summary and repository map. |
| `IMPLEMENTATION_PLAN.md` (root) | Ordered delivery plan and package sequencing. |
| `IMPLEMENTATION_STATUS.md` (root) | Long-form implemented-vs-designed gap analysis. |
| `docs/IMPLEMENTATION_STATUS.md` | Concise milestone execution ledger. Different document from the root file despite the shared name — both are current, neither supersedes the other. |
| `docs/PACKAGE3_KIT_AUDIT.md` | The 36-slot character-kit audit and conversion order. |
| `docs/PACKAGE4_ROSTER_PLAN.md` | The enemy/boss roster plan: archetype system, per-era roster IDs, boss kits, and the per-workstream deviation log. |
| `docs/PACKAGE5_CAMPAIGN_PLAN.md` | The campaign plan for levels 2–15: per-level dossiers with the locked encounter economy, the authored boss stat table (§4.1), the shared-file conflict policy, crash hygiene (§2.8), and a long per-level deviation log (§9). Complete; read §9 before touching a campaign level. |
| `docs/PACKAGE6_FIGHTER_PLAN.md` | The ten-stage local Fighter Mode plan: standing decisions (§2), the per-stage geometry dossiers and the ten era-hazard specs (§4/§4.1), the Phase A/B/C workstream split, and a long per-workstream deviation log (§9). Complete; read §9 before touching Fighter stages, hazards, match flow, the CPU, or the rollback harness. |
| `docs/DUST_ECONOMY.md` | The Chronal Dust reward/cost model. Locked by `tests/ContentValidation/DustEconomyTests.cs`. |
| `docs/architecture/000*.md` | Accepted ADRs. Supersede rather than silently rewrite one. |
| `docs/BUILDING.md`, `docs/PERFORMANCE_BASELINE.md` | Build/validation procedure and the Package 0 performance baseline. |
| `resources/**/*.tres` | Canonical runtime tuning. Numbers in code or docs never override a resource. |

**Superseded but Godot-era — context only:**

- `docs/development-plan.md` — an earlier 12-stage Godot plan. `IMPLEMENTATION_PLAN.md` is the live plan; use this only for background on how the phasing was originally reasoned about.

**Removed on 2026-08-06.** The Unity-era design archive (`design.md` and its rendered `html/` build, `character_base_stats.md`, `ability_numeric_data.md`, `enemy_and_boss_numeric_data.md`, `technical_implementation_guide.md`, `sprite_generation_pipeline_plan.md`, `design_gaps_analysis.md`, `design_gaps_resolved.md`, `chat-history.md`) and the stale duplicate `docs/design-godot.md` are no longer in this repository. They described a Unity `ScriptableObject` project with a ten-character roster and were a live source of wrong numbers. Copies remain outside the repo in `D:\Projects\fighters-through-time-docs-2\`. Do not reintroduce them into `docs/`, and do not cite them as design authority if you encounter them there.

If a document and a `.tres` resource disagree, the resource wins. If two documents disagree, follow `AGENTS.md`'s rule: surface the conflict rather than silently normalizing it.

## Working agreements

- **Don't touch `.godot/`.** It is generated cache and build output. Legacy files under it are already tracked in git; do not add, remove, or clean them without explicit authorization.
- **Keep `.uid` sidecars with their scripts.** Godot generates them; they are project metadata, not noise.
- **Resources own the numbers.** Put static tuning in `.tres`, runtime state in controllers/snapshots. Never create a second canonical value in a factory, UI script, or document.
- **Story must not leak into Fighter.** Resonance grid perks, difficulty scaling, and story progression modifiers stay out of shared stats, ability resources, and the deterministic simulation.
- **Determinism is a hard boundary.** `scripts/FighterSim/` runs on Klotho fixed-point math (`FP64`/`FPVector2`). Never introduce float math, `PhysicsServer2D` authority, `Date`/`Random` without a seeded deterministic source, or Godot state feeding back into simulation there.
- **Localize everything visible.** New user-facing copy uses a translation key plus an entry in `localization/en.csv`.
- **Finish the validation loop before reporting done.** At minimum `dotnet build`; add `dotnet test` for anything touching gameplay, simulation, saves, pools, collision, or localization; add the headless import check for scene/resource edits. Report the actual result, including failures.
- **Update `AGENTS.md` when reality changes.** Closing one of its known-gap bullets, changing an autoload, adding a system, or shifting the test baseline all require an edit there in the same change.
- **Preserve unrelated working-tree changes.** This tree usually has uncommitted work in it.

## Where to start, by task type

| Task | Start here |
|---|---|
| Character ability | `docs/PACKAGE3_KIT_AUDIT.md` → `resources/Abilities/` → `scripts/Characters/Abilities/` → `scripts/FighterSim/` for the Fighter-side equivalent |
| Fighter simulation / netcode | `docs/architecture/0002-*.md`, `docs/architecture/0003-*.md` → `scripts/FighterSim/` → `scripts/Networking/` |
| Story level or hub content | `design-godot.md` Sections 2–3, 6–10 → `scripts/Environment/` → `scenes/campaign/` |
| Saves | `docs/architecture/0004-*.md` → `scripts/Core/SaveManager*` |
| UI / dialogue | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 → `resources/UI/ftt_theme.tres` + `scripts/UI/UIPalette.cs` → `scenes/ui/`, `scripts/UI/`, `resources/Dialogue/`, `localization/en.csv`. Adopt the theme on the screen root; author focus with `FocusChainBuilder`; store raw keys in `.tscn` `text` and let control auto-translation resolve them |
| Audio | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A2/B5 → `resources/Audio/default_bus_layout.tres` → `scripts/Core/AudioManager.cs` (`StemDirector`, `AudioSnapshotMixer`) → `resources/Audio/*_audio.tres`. The stem mix is **additive** |
| Visual / VFX / shaders | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A3/B6/B7 → `assets/shaders/outline_glow.gdshader` → `scripts/Combat/GlowPresentationController.cs`, `VfxEmitter`, `ParticleBudget` → `scenes/vfx/`. `gl_compatibility` forbids `instance uniform`s and `TEXTURE` as a `sampler2D` argument |
| Input remapping / settings | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A4 → `scripts/Core/InputBindings.cs` → `scripts/UI/SettingsMenu.cs` + `scenes/ui/Settings.tscn` → `scripts/Core/SaveManager*` (global payload schema v4) |
| Enemy or boss content | `docs/PACKAGE4_ROSTER_PLAN.md` → `resources/Enemies/`, `resources/Bosses/` → `scripts/Enemies/` → `resources/Content/content_manifest.csv` + `localization/en.csv` |
| Collision changes | `project.godot` layer names + `CollisionLayers` constants + scene masks + tests, all together |
