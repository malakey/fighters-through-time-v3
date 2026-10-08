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

**Paths corrected 2026-09-13 (Package 11 Phase C).** A 2026-08-15 note here claimed the repository and both Godot executables had moved to `C:\Users\DavidMcClelland\Documents\FTT\` and that `D:\Projects\` "no longer exists on this machine". **That is wrong on the current machine** — a Package 11 agent flagged it, and everything lives under `D:\Projects\`, which is exactly what `.claude/settings.json` sets `GODOT_BIN` to. Read any `C:\Users\DavidMcClelland\Documents\FTT\...` path in older `docs/` prose as `D:\Projects\...`. If `dotnet test` reports `The Godot executable was not found at path: …` together with `No test is available`, that is a stale `GODOT_BIN`, not a broken suite.

`GODOT_BIN` is set to the console executable by `.claude/settings.json`, so `dotnet test` picks it up without extra setup in a Claude Code session. Outside a Claude session it must be exported manually.

Both Godot executables live one directory above the repository, in `D:\Projects\`. They are not in the repo and not on `PATH`.

`GodotSharp\` must stay next to the executables — it is the .NET API assembly directory that ships with the Godot Mono build, and Godot resolves it relative to the binary, not the project.

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
- **Double-disposed C#-scripted `RefCounted`** (Package 5 A close). The tell is in the Godot log, not the runner output: `ERROR: FATAL: Condition "gchandle.is_released()" is true. at: mono_object_disposed_baseref` with a `GC.RunFinalizers -> GodotObject.Finalize -> Dispose` backtrace. That `CRASH_COND` compiles to `ud2` on this MinGW build, which *is* the `0xC000001D` exit. Three ways to cause it: calling `Dispose()` on a Godot `Resource` (never do this — reference counting owns them); letting an authored `.tres` graph cycle in and out of the resource cache so its script instances are rebuilt and reaped repeatedly (load through `FTT.Core.AuthoredResources` instead); and a scripted `.tres` that a **scene file** assigns to an export, which reaches C# without that cache. The third was found on 2026-10-04: `ContentTemplateMarker.Contract` on every enemy, checkpoint and template scene, and `ChronalDustPickup.VisualTiers`. A level teardown left the contract's wrapper collectable, and the next scene load took the cached native resource back before the finalizer ran. It crashed about one full run in three, always right after `LevelDeathRewindDiagnosticTests`' Level 2 teardown. Such an export's setter must call `AuthoredResources.Pin`, and `tests/ContentValidation/SceneAssignedResourcePinTests.cs` fails on a scene that assigns a scripted resource through any other property. See `docs/PACKAGE5_CAMPAIGN_PLAN.md` §9 and `docs/PLAYTEST_FEEL_2026-10-04_PLAN.md` §3.

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
| 7. Pure-C# suite touched the engine | a plausible `Passed!` line, then **`The active test run was aborted`** | `0xC0000005` | A `[TestSuite]` without `[RequireGodotRuntime]` called a Godot API; see below |

### Failure signature 7: a crashed test host after a plausible `Passed!` (a pure-C# suite touched the engine)

Discovered 2026-09-13 by Package 11 A6b. A `[TestSuite]` **without** `[RequireGodotRuntime]` runs in a plain .NET host with no Godot runtime, where any call into the engine — `Godot.FileAccess`, `ResourceLoader`, `ProjectSettings`, reading the content manifest — is an **access violation**, `0xC0000005` in `godotsharp_string_new_with_utf16_chars`. It is **not catchable**: `try`/`catch` cannot see a native AV.

```text
Passed!  - Failed: 0, Passed: N, ...
The active test run was aborted. Reason: Test host process crashed : Fatal error.
```

The summary line is printed **before** the abort, so the run looks green-ish at a glance and reads like signature 5. It is not: read the `Aborted` / `crashed` line and the managed stack, which names the exact test and the exact static initializer that reached the engine.

Two instances were found and fixed in the same pass, both from a field initializer and then from `Normalize()` on `GlobalSaveData` — which is why **save payload classes must stay plain data with no engine dependency** (AGENTS.md's coding conventions). The same caution applies to `CharacterRoster`, `CampaignCaptiveRoster` and `ContentManifest.LoadDefault()`.

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
across checkouts. Since Package 13 every checkout serializes on a **lock directory**: acquire it with
`mkdir "D:/Projects/.ftt-gdunit-lock"` before any `dotnet test` (retry while it exists; if it is older than
20 minutes and no `testhost`/`Godot*` process runs, remove it) and `rmdir` it immediately after the run,
even on failure. Drain `testhost` processes first (`Get-Process testhost`) and wait for the count to
reach zero — polling for a clear window proved far more reliable than blind retries. **Never accept a
full-suite number without comparing it to the exact expected total**; "Passed!" plus a non-trivial
count is worthless on its own.

### Order-dependent movement flakes: out-of-band `MoveAndSlide` (fixed by `--fixed-fps 60`)

Root-caused 2026-09-27 by Package 12 W9. A suite that steps a body by calling
`_PhysicsProcess(Step)` directly gets a fixed 1/60 for every state timer, but
`CharacterBody2D.MoveAndSlide()` reads the **idle frame's** delta whenever the engine is
not inside a physics frame — the wall-clock length of the runner's previous frame, measured
at 0.0003–0.0174 s across one run. Walk-to loops then moved 58× too slowly depending on test
order ("expected Patrol got Returning", a player that never lands). `.runsettings` now passes
`--headless --fixed-fps 60`, which pins that delta to exactly 1/60 (and cut the full suite from ~100 s to ~70 s). **Do not remove the flag.** It does not conflict with W6's 60 FPS render cap: `DisplayTimingRules.EffectiveMaxFps` leaves a headless process uncapped.
A case that must also be robust without it can run its body inside a physics frame through
`tests/Unit/OutOfBandPhysicsStep.RunInPhysicsFrameAsync` (GdUnit requires `async Task`).

### Known smoke-run noise: `Handle is not initialized` on hub boot (pre-existing, open)

Found at the Package 12 Phase C closeout (2026-09-27). A `HubWorld.tscn --quit-after 300` smoke run sometimes logs
`System.InvalidOperationException: Handle is not initialized` from `ScriptManagerBridge.SwapGCHandleForType`, inside
`DialogueManager.CreateDefault` (`ResourceLoader.Load` / `PackedScene.Instantiate` of `scenes/ui/DialogueBox.tscn`). The
run still exits 0 and falls back to the code-built dialogue box. **It depends on what is in `user://saves`**: never with an
empty saves directory, roughly 1 run in 3 to 1 in 13 with a populated one, and it reproduces on the pre-Phase-C commit
`2477875` — it is not a Phase C regression. When you see it, reset or restore `user://saves` before blaming your change,
and compare against a control run with the same saves. Diagnosis and the A/B numbers are in
`docs/PACKAGE12_DESIGN_ALIGNMENT_PLAN.md` §9 (Phase C). The GdUnit suite never hits it.

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

Build (verified 2026-09-30 at the Package 13 closeout: a **full** rebuild succeeds with 0 errors and three pre-existing warnings, none caused by your change — `CS8632` in the vendored `addons/gdunit4/src/dotnet/GdUnit4CSharpApi.cs`, Klotho's `KLSG_ECS004` on `FighterTuningComponent` exceeding 128 bytes (a static tuning struct, present since August), and `CS0649` on the never-assigned `InputManager.BlockModeOverrideForTesting` test seam (Package 12 W6). An incremental build often reports 0. The old `CS9057` analyzer-version warning disappeared with the move to SDK 10. **A `KLOTHO_DET002` warning is never pre-existing**: it means float math reached `scripts/FighterSim/`. Package 13 W6 introduced two through a float overload of `UltimateActivationRules.StatusRidesFinale`, and Phase C removed them with a bool overload — treat a new one as a determinism bug, not noise):

```bash
dotnet build FightersThroughTime.csproj --nologo
```

Full headless test suite (GdUnit4 spawns Godot itself; `.runsettings` forces serial headless execution and passes `--headless --fixed-fps 60` — see the movement-flake note above). **Verified baseline: 2711**, five consecutive green runs on 2026-10-07 after the 2026-10-04 playtest feel pass and its fix round were merged (orphan processes drained and the lock directory held for each). The arithmetic (2561 + 99 from round 1 + 51 from the fix round) is in `AGENTS.md`'s validation section and `docs/PLAYTEST_FEEL_2026-10-04_PLAN.md` §3 — always reconcile any delta against it before accepting a total. Read the `Total:` count in the summary, not just the exit code — see the failure signatures above:

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

### Automated Story playtests (bots)

`res://scenes/diagnostics/PlaytestRunner.tscn` plays a campaign level with a scripted bot on player 0 (through `InputManager.SetInputSource`, the Mirror Paradox seam) and writes a JSON telemetry report to `user://playtest/`. It uses the slotless developer direct-launch and suppresses save writes, so it never touches a save. `--fixed-fps 60` keeps every frame exactly 1/60 s, and a run is faster than real time (a completed Easy level is typically 2–3 game minutes). Code lives in `scripts/Diagnostics/`; the hero runs the **campaign kit** by default (Legacy Unlock locks as a real run at that level would have them).

Bots — all of them navigate with the same nav graph, strike checkpoints, solve the level's puzzles, beat the boss and seal the level; only the fighting differs:

- `play` — a competent player: string, unlocked Specials and Ultimate, blocks telegraphed swings, retreats at low HP, avoids projectiles and hazards. **This is the bot that finishes levels** and the one to read a level's difficulty from.
- `spam` — 3-hit string + Special 1 + Special 2 the moment they are ready, never blocks.
- `basics` — string only, blocks enemy swings.
- `bypass` — runs past everything, breaks only what blocks it, fights only the boss.
- `feel` — the frame-exact feel probe (`PlaytestFeel.cs`, `tools/playtest/analyze_feel.py`), not a level runner.

```powershell
./tools/playtest/run_playtests.ps1 -Levels 2,3,4,5,6,7,8,9,10,11,12 -Characters joan -Bots play -Difficulty easy -Parallel 8 -MaxSeconds 0 -Tag e1
```

`-MaxSeconds 0` sizes each run to the level's Timeline Integrity budget on that difficulty (par × 2.0 / 1.5 / 1.2) plus a minute; the default is 600. `-ExtraArgs "--trace-every=20","--nav-dump=1"` passes runner options through. The script writes `summary_<difficulty>_<tag>.csv` (end reason, seconds, checkpoints, boss down, deaths) beside the reports, and one log per run under `playtest/logs/`.

Single run: `"D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --fixed-fps 60 --path "D:\Projects\Fighters Through Time - V3" res://scenes/diagnostics/PlaytestRunner.tscn -- --level=7 --character=joan --bot=play --difficulty=easy --tag=t1`. Runner options: `--level`, `--character`, `--bot`, `--difficulty`, `--max-seconds` (0 = auto), `--tag`, `--campaign-kit=0` (full kit), `--nav-dump=1` (write the nav graph's segments and edges to `user://playtest/nav_<run>_start.txt` / `_end.txt`), `--trace-every=N` (trace period in frames, default 60), `--debug-ropes=1` (a `nav` note every 6 hung frames saying why the planned rope release was refused), `--seed=N` (runs are otherwise frame-identical replays; a non-zero seed holds neutral input for a seed-derived 5–60 frames before the bot takes control and appends `_s<N>` to the run name, so repeated samples of one level desynchronize from the world; 0 is the classic run — pass it to the batch script through `-ExtraArgs`), `--capture=1` (the feel probe's frame captures; needs a window).

**Reading a report.** `end_reason` is `level_complete`, `timeout`, `left_level:collapse(<cause>):<scene>` (a Timeline Collapse to the hub, or Game Over in Act III), `stuck_paused:<what>` (the tree stayed paused for 30 s with nothing the runner can advance) or `fell_out_of_world@x,y`. The report carries `damage_by_source` (enemy / hazard / extractor / escape / searchlight / environment), `deaths_detail` with positions, checkpoint / boss-defeated / seal frames, every dialogue sequence ID, the bot's `nav` notes (rope releases, banned edges, give-ups), periodic puzzle snapshots (plates, weights, coils, locks, breakables), and a trace with position, velocity, state, the bot's current task and nav edge. `python tools/playtest/trace.py L07_joan_play_easy_t1 --from 20 --to 40` prints it compactly. `python tools/playtest/compare_runs.py <tagA> <tagB> ...` compares batches: for each tag (a glob such as `"before*"` also matches its seeded runs) it prints one table per bot — runs, completions, boss down, mean deaths and damage taken, standard mobs killed and their median frames to kill, 2-second burst kills, Special-majority kills, elite median kill time, mobs passed alive and the hits they landed afterwards.

**How the bots move.** `PlaytestNav` builds a platform graph from the scene's collision shapes (rects, circles, polygons; one-way platforms, moving platforms and trapdoors refreshed every frame, statics cached) and plans with Dijkstra over walk / fall / jump / double-jump / drop-through / break / ride / rope-catch / rope-release edges. Every jump and rope release is **simulated frame by frame** against a copy of `PlayerController`'s air physics before it is taken, so a planned jump either lands or is not attempted. Interactables are driven through `InteractionArea.TryInteract`, because `InteractionArea` only listens to raw `_UnhandledInput` and the bot's input source never produces one. Per-level scripted help lives in `PlaytestRouteHints` (empty today). Before reading a death or a stall as a balance signal, read the trace and the `nav` notes to confirm the bot was not simply lost — and check the known game bugs the bots have surfaced: swinging-rope bars are still solid floor (`PendulumAnchorTemplate` is an Environment-layer `AnimatableBody2D` with a 16 × 320 rope shape), which with the audience throws keeps Level 10's rope galleries unreliable for the bots; and on Level 6 the bot does not stand on the east pan as the winch's missing counterweight, so it stalls at the rockfall and collapses on the timer (a bot gap — the game side was fixed on 2026-10-04). The old "Story drop-through only works from Block or an attack" bug is **fixed** (fix round G7: a double-tap of Down drops from Idle and Running too); the bots still tap from Block, which keeps working. `EventBus.OnDialogueTriggered` is an inbound request channel that nothing raises, so the runner polls `DialogueManager.ActiveDialogueID` for the dialogue events. The full list and dispositions are in `docs/PLAYTEST_FEEL_2026-10-04_PLAN.md` §3.

Useful scene targets: `res://scenes/menus/MainMenu.tscn` (main scene), `res://scenes/arenas/TestArena.tscn` (Fighter sandbox), `res://scenes/campaign/HubWorld.tscn` and `res://scenes/campaign/Level_NN_*.tscn` (all sixteen campaign levels are authored; the Level 4A variants were deleted by Package 13), `res://scenes/fighter/FighterStage_*.tscn` (all ten production Fighter stages are authored — Paris/Vesuvius/Nassau are the three Open ones), `res://scenes/ui/HolodeckDrill.tscn` (the Calibration Drill sandbox), `res://scenes/ui/GameOver.tscn` (the Smothered surface), `res://scenes/diagnostics/PerformanceBaselineRunner.tscn` (performance baseline runner).

A campaign scene's `--quit-after` smoke run normally prints `N resources still in use at exit`. That is the `AuthoredResources` process-lifetime cache releasing at quit, it is **nondeterministic across runs of the identical build**, and an untouched control level prints it too — check a control scene before treating it as a leak you introduced.

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
- Any Godot launch (a test run, `--import`, a smoke run) rewrites the Tubman `*.png.import` sidecars under `assets/` with LF endings, so they show as modified with an empty `git diff`. That is line-ending churn, not a change: `git checkout -- assets` and move on. Never commit it.

## Documentation authority map

`docs/` now holds only current Godot-era material; the legacy Unity archive was removed on 2026-08-06 (see below).

**Read the authority rule in `AGENTS.md` first.** Since 2026-09-13 (P04) the GDD and the adopted contracts under `docs/design-contracts/` define *intended behaviour*, and code plus `.tres` data describe the *current build*. Where they differ, the ledger — `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` — records it. "Never a second canonical value" still holds: tuning lives in one place.

**Authoritative — implement against these:**

| Path | Role |
|---|---|
| `design-godot.md` (repository root) | The full product and technical specification (V7.6 + the F01–F24 resolutions + the 2026-09-27/29 story, ability and character reviews). This file is a **synced mirror** — the canonical master lives at `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`; edit the master and re-copy it here. **One known master defect (Package 13 Phase 0):** the S14 rename "Unbound → Severed" was a find-and-replace that also hit the *input-binding* sense of "Unbound" — the Controls table's Ultimate / Grab / Echo Step rows ("Direct Severed") and the C01c remapping bullet, plus `COMFORT_SETTINGS.md` lines 101–103, 128, 130 and 136. **The repo mirror and the repo contract copy restore "Unbound" there**, so on exactly those lines the mirror deliberately differs from the master; diff them before any re-sync (`DOC-MASTER-UNBOUND-BINDING`). The build's binding sense (`InputBindings.SetUnbound`, `controls_binding_unbound`) is correct and must never be renamed. |
| `docs/design-contracts/*.md` | The **adopted decision contracts** — `DEFENSIVE_EFFECTS` (D01–D04), `TEMPORAL_STATE_CONTRACT` (F03/T01), `STORY_PERSISTENCE` (F10), `CHECKPOINT_RECOVERY` (F11), `FIGHTER_MATCH_RULES` (F21/F22), `HUD_CONTRACT` (F24), `COMFORT_SETTINGS` (C01a/b/c), `CPU_RECOVERY` + `CPU_COMBAT_POLICY` (F19), `DUST_ECONOMY` (F05, with the S27 amendment), `ABILITY_DATA` (A15 — the per-ability numeric contract the Package 13 kits were built against), `MIRROR_PARADOX` (F20), `NARRATIVE_RESOLUTION` (N01–N05), `CAMPAIGN_VALIDATION` (V01/V02), `COMBAT_VALIDATION`, `ROLLBACK_STATE_CONTRACT` (S01), `MULTIPLAYER_DELIVERY`, `PRODUCTION_SCOPE` (P02). These define intent and supersede the historical rule they identify. **`LEGACY_LEVELS` and `LEGACY_CHECKPOINTS` (F12's Level 4A half) are retired history** since S27 retired Level 4A — they carry a retirement banner and describe deleted code. |
| `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` | **The live deviation ledger.** Every open `VERIFY-*` / `DEFER-*` entry with its owner and acceptance criteria. Read it before assuming a shipped value is the target. |
| `AGENTS.md` | Durable architecture/context summary and repository map. |
| `IMPLEMENTATION_PLAN.md` (root) | Ordered delivery plan and package sequencing. |
| `IMPLEMENTATION_STATUS.md` (root) | Long-form implemented-vs-designed gap analysis. |
| `docs/IMPLEMENTATION_STATUS.md` | Concise milestone execution ledger. Different document from the root file despite the shared name — both are current, neither supersedes the other. |
| `docs/PACKAGE3_KIT_AUDIT.md` | The 36-slot character-kit audit and conversion order. |
| `docs/PACKAGE4_ROSTER_PLAN.md` | The enemy/boss roster plan: archetype system, per-era roster IDs, boss kits, and the per-workstream deviation log. |
| `docs/PACKAGE5_CAMPAIGN_PLAN.md` | The campaign plan for levels 2–15: per-level dossiers with the locked encounter economy, the authored boss stat table (§4.1), the shared-file conflict policy, crash hygiene (§2.8), and a long per-level deviation log (§9). Complete; read §9 before touching a campaign level. |
| `docs/PACKAGE6_FIGHTER_PLAN.md` | The ten-stage local Fighter Mode plan: standing decisions (§2), the per-stage geometry dossiers and the ten era-hazard specs (§4/§4.1), the Phase A/B/C workstream split, and a long per-workstream deviation log (§9). Complete; read §9 before touching Fighter stages, hazards, match flow, the CPU, or the rollback harness. |
| `docs/PACKAGE8_PRESENTATION_PLAN.md` | The presentation package: theme/palette pairing, focus authoring, audio framework, VFX taxonomy, and its §9 deviation log. |
| `docs/PACKAGE11_V7_6_ALIGNMENT_PLAN.md` | **The V7.5/V7.6 alignment package.** §2 standing decisions (authority, boss-HP scope, legacy-identifier retention, the save bump, the Klotho ID table, enum contracts, EventBus decoupling, dialogue schema, `en.csv` policy, test discipline, crash hygiene, Story/Fighter isolation), §3/§4 the twenty workstreams, §6 the closeout, §8 what stayed out of scope, **§9 the per-workstream deviation log**, §10 the closeout report. Read §9 before touching anything this package built. |
| `docs/PACKAGE12_DESIGN_ALIGNMENT_PLAN.md` | **The 2026-09-26 design-alignment package.** §2 the decisions (D1–D15) and §2.1 which were adopted and which are **held** for a user ruling, §3 standing decisions (the single v7 save bump, component 320, Story/Fighter isolation, legacy identifiers), §5/§6 the eleven workstreams, §9 the per-workstream deviation log, §10 the closeout report and the honest not-delivered list. Read §2.1 and §9 before touching anything this package built — and never touch a held item. |
| `docs/handoffs/P12_*.md` | The eleven Package 12 workstream handoffs (W1–W10 plus W3b): exact API names, declared v7 derivations, hash moves, test deltas, and what did not ship. |
| `docs/PACKAGE13_REVIEW_ALIGNMENT_PLAN.md` | **The 2026-09-29 review-alignment package** — the story review (S01–S49), the ability review (A01–A15) and the character review, including **Pocahontas → Harriet Tubman**. §0 the sources and the master's binding-rename defect, §2 the adopted decisions D1–D16 (4A deletion, the v8 derivations, Tubman's IDs, components 321/322, D9's out-of-scope sim Special phases), §3 standing decisions, §5–§7 the eight workstreams, **§9 the per-workstream deviation log**, §10 the closeout report and the honest not-delivered list. Read §2 and §9 before touching anything this package built. |
| `docs/handoffs/P13_*.md` | The eight Package 13 workstream handoffs (W1–W6, W7a, W7b): exact API names, declared v8 derivations, component layouts, hash moves, test deltas, provisional numbers, and what did not ship. |
| `docs/handoffs/P11_*.md` | The twenty-one Package 11 workstream handoffs — exact API names, what shipped, what did not, and why. The most specific record of any Package 11 system. |
| `docs/recon-2026-09-13/*.md` | The nine reconnaissance dossiers the package was authored from (~600 KB). Evidence about the pre-package build, not a target. |
| `docs/DUST_ECONOMY.md` | A **synced mirror** of `docs/design-contracts/DUST_ECONOMY.md` (F05) plus an implementation map saying where each rule lives in code. Edit the contract, not the mirror. Locked by `tests/ContentValidation/DustEconomyTests.cs` and `RewardManifestTests`. |
| `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` | The 2026-08-10 gameplay-feel batch: locked movement/combat retune decisions (run/jump/damage numbers, block-cancel, HP-scaled knockback, directional attacks, fast-fall, sim ledge grab) and its §9 deviation log. Read §2/§9 before touching movement or basic-combat feel. |
| `docs/architecture/000*.md` | Accepted ADRs. Supersede rather than silently rewrite one. |
| `docs/BUILDING.md`, `docs/PERFORMANCE_BASELINE.md` | Build/validation procedure and the Package 0 performance baseline. **The baseline's resim/snapshot figures predate Package 11's component growth** and need re-measuring as Package 7 entry work. |
| `resources/**/*.tres` | Canonical runtime **tuning storage**: one value, in one place, loaded through `AuthoredResources`. Evidence about the build rather than design authority — see the authority rule above. |

**Analysis and review — context, not authority:**

- `docs/DESIGN_ANALYSIS_2026-08-16.md`, `docs/DESIGN_ANALYSIS_2026-09-08.md`, `docs/DESIGN_REVIEW_V7_6_2026-09-11.md`, `docs/IMPLEMENTATION_ANALYSIS_2026-08-16.md`, `docs/IMPLEMENTATION_AUDIT_2026-08-08.md` — dated snapshots of what the build looked like at a moment. Useful leads; superseded wherever a later contract, plan or ledger entry says otherwise.

**Superseded but Godot-era — context only:**

- `docs/development-plan.md` — an earlier 12-stage Godot plan. `IMPLEMENTATION_PLAN.md` is the live plan; use this only for background on how the phasing was originally reasoned about.

**Removed on 2026-08-06.** The Unity-era design archive (`design.md` and its rendered `html/` build, `character_base_stats.md`, `ability_numeric_data.md`, `enemy_and_boss_numeric_data.md`, `technical_implementation_guide.md`, `sprite_generation_pipeline_plan.md`, `design_gaps_analysis.md`, `design_gaps_resolved.md`, `chat-history.md`) and the stale duplicate `docs/design-godot.md` are no longer in this repository. They described a Unity `ScriptableObject` project with a ten-character roster and were a live source of wrong numbers. Copies remain outside the repo in `D:\Projects\fighters-through-time-docs-2\`. Do not reintroduce them into `docs/`, and do not cite them as design authority if you encounter them there.

If a document and a `.tres` resource disagree, **the document's intent wins and the resource is evidence** — close it by changing the build, or record it in `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` with an owner and acceptance criteria. This replaces the pre-Package-11 rule ("the resource wins"), which quietly promoted every shipped value into the design. If two documents disagree, a more specific adopted resolution supersedes the historical rule it identifies; otherwise surface the conflict rather than silently normalizing it.

**One correction to record:** `docs/PACKAGE5_CAMPAIGN_PLAN.md` **is present in this repository**. The docs-workspace source table in `DESIGN_BUILD_DEVIATIONS.md` and the master both flag it as missing; that flag is wrong from the repo's side, and no recovery work is needed.

## Working agreements

- **Don't touch `.godot/`.** It is generated cache and build output. Legacy files under it are already tracked in git; do not add, remove, or clean them without explicit authorization.
- **Keep `.uid` sidecars with their scripts.** Godot generates them; they are project metadata, not noise.
- **One canonical value, and the design defines intent.** Put static tuning in `.tres` (or a single named rulebook constant) and runtime state in controllers/snapshots; never create a second canonical value in a factory, UI script, or document. The resource is where the number *lives*, not what makes it *right* — see the authority rule.
- **Story must not leak into Fighter.** Resonance grid perks, difficulty scaling, the Legacy Unlock Schedule, `Suppression`, Time Freeze and story progression modifiers stay out of shared stats, ability resources, and the deterministic simulation.
- **Save payload classes stay engine-free.** No `res://` read, resource load or manifest read from a `GlobalSaveData` / `StorySaveData` field initializer or `Normalize()` — a pure-C# suite that constructs one has no Godot runtime, and that is failure signature 7.
- **Determinism is a hard boundary.** `scripts/FighterSim/` runs on Klotho fixed-point math (`FP64`/`FPVector2`). Never introduce float math, `PhysicsServer2D` authority, `Date`/`Random` without a seeded deterministic source, or Godot state feeding back into simulation there.
- **Localize everything visible.** New user-facing copy uses a translation key plus an entry in `localization/en.csv`.
- **Finish the validation loop before reporting done.** At minimum `dotnet build`; add `dotnet test` for anything touching gameplay, simulation, saves, pools, collision, or localization; add the headless import check for scene/resource edits. Report the actual result, including failures.
- **Update `AGENTS.md` when reality changes.** Closing one of its known-gap bullets, changing an autoload, adding a system, or shifting the test baseline all require an edit there in the same change.
- **Preserve unrelated working-tree changes.** This tree usually has uncommitted work in it.

## Where to start, by task type

| Task | Start here |
|---|---|
| Character ability | `docs/design-contracts/ABILITY_DATA.md` (A15) → `docs/handoffs/P13_W7a.md` / `P13_W7b.md` / `P13_W5.md` (the current kits) → `resources/Abilities/` → `scripts/Characters/Abilities/` → `scripts/FighterSim/` for the Fighter-side equivalent. Sim Specials still fire on press (`DEFER-SIM-SPECIAL-PHASES`), so a new startup number is Story-only until that closes. `docs/PACKAGE3_KIT_AUDIT.md` is the older 36-slot audit |
| Ultimate / activation strike | `docs/handoffs/P13_W6.md` → `scripts/Combat/UltimateActivationRules.cs` (D15 totals, shared by both modes) → `scripts/FighterSim/FighterUltimateActivation.cs` (component 321) + `FighterUltimateRules.cs`. Story keeps screen-clearing Ultimates; only the Mirror clone plays the strike |
| Roster change (adding, replacing or retiring a character) | `docs/handoffs/P13_W5.md` is the worked example (Pocahontas → Tubman). `resources/Content/content_manifest.csv` (grid order) + `FTT.Core.CharacterRoster`; **`FighterCharacterID` is append-only** (retire an ordinal as `[Obsolete]`, never reuse it) and serializes, so bump `RollbackInputPacket.ProtocolVersion`; retire the character's persistent/zone type IDs and kit phases; declare an engine-free save derivation (`RosterSwapV8` is the pattern) for the next package's single bump; placeholder art through `tools/generate_assets.gd` and `tools/build_retro_character_spriteframes.py` |
| Fighter simulation / netcode | `docs/architecture/0002-*.md`, `docs/architecture/0003-*.md`, `docs/design-contracts/ROLLBACK_STATE_CONTRACT.md` → `scripts/FighterSim/FighterStageGeometry.cs` for stage layout → `scripts/FighterSim/` → `scripts/Networking/` (protocol **v4**). The Klotho component inventory is in `AGENTS.md` (highest ID **322**; kit phases 19–20, persistent type 4 and zone type 83 are retired); **never allocate an ID without checking it**, and a `KLOTHO_DET002` build warning is a determinism bug |
| Fighter menu / Versus CPU / match exits | `docs/handoffs/P12_W5.md` → `scripts/UI/FighterFlowRoutes.cs` (every exit, by `SessionData.FighterMatchOrigin`) → `scenes/menus/FighterPlayOptions.tscn` → `CharacterSelectScreen` + `HolodeckConsolePanel` (`FrontEnd`). Hazards are an On/Off toggle with a per-stage cadence in `FighterHazardCadence` |
| Fighter CPU | `docs/design-contracts/CPU_RECOVERY.md` + `CPU_COMBAT_POLICY.md` → `scripts/FighterSim/FighterCpuController.cs` (`CpuBandTuning`) + `CpuRecoveryProfile.cs`. Band tuning is **code-owned and must never become a resource** |
| Story level or hub content | `design-godot.md` Sections 2–3, 6–10 → `docs/design-contracts/CAMPAIGN_VALIDATION.md` → `docs/handoffs/P13_W3.md` (Level 0, hub, Act III, ending) / `P13_W4.md` (Levels 1–12) / `P13_W2.md` (the 16-slot route, Level 5's Eraser debut) → `scripts/Environment/StoryLevelControllerBase.cs` (+ `.Narrative.cs` for absence beats) → `scenes/campaign/`. Level 4A is deleted; `CampaignLevel.LegacyNexus = 16` is a reserved ordinal |
| Time Freeze / the temporal layer | `docs/design-contracts/TEMPORAL_STATE_CONTRACT.md` → `scripts/Environment/TimeFreezeController.cs` → `IStoryTimeFreezable` implementers |
| Timeline Integrity / checkpoints / recovery | `docs/design-contracts/CHECKPOINT_RECOVERY.md` → `scripts/Core/TimelineIntegrityRules.cs` → the `StoryManager` Integrity clock → `CollapseTremorController` |
| Defence, shields, Defy | `docs/design-contracts/DEFENSIVE_EFFECTS.md` → `scripts/Combat/StoryDefense.cs` + `PlayerController.ResolveProtectionLayers` → `FighterDefenseRules` (component 312) |
| Saves | `docs/architecture/0004-*.md` + `docs/design-contracts/STORY_PERSISTENCE.md` → `scripts/Core/SaveManager*`, `SaveEnvelope.cs` (schema v8; `SaveSchemaV8MigrationTests` pins the v7→v8 step, `SaveSchemaV7MigrationTests` the v6→v7 one), `StoryAttemptState.cs`. A new derivation is declared by the owning workstream and composed by the next package's single bump — never a bump of your own |
| Dust / rewards | `docs/design-contracts/DUST_ECONOMY.md` (S27: 15 bosses, Level 5 = 55/25/30) → `resources/Content/reward_manifests/` (16 manifests) → `LevelRewardDirectory` / `RewardAllocator` |
| UI / dialogue | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 → `resources/UI/ftt_theme.tres` + `scripts/UI/UIPalette.cs` → `scenes/ui/`, `scripts/UI/`, `resources/Dialogue/`, `localization/en.csv`. Adopt the theme on the screen root; author focus with `FocusChainBuilder`; store raw keys in `.tscn` `text` and let control auto-translation resolve them |
| Audio | `docs/design-contracts/COMFORT_SETTINGS.md` (C01b) + `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A2/B5 → `resources/Audio/default_bus_layout.tres` → `scripts/Core/AudioManager.cs` (`StemDirector`, `AudioSnapshotMixer`) → `resources/Audio/*_audio.tres`. The stem mix is **additive**; the snapshot mixer is a **priority selector**, not a stack |
| HUD | `docs/design-contracts/HUD_CONTRACT.md` (F24) → `scenes/ui/StoryHUD.tscn` + `scripts/UI/StoryHUD.cs` → `scripts/UI/RadialProgress.cs`. Publishers never edit the HUD — they append a typed payload to `scripts/Core/EventBus.cs` and the HUD subscribes |
| Visual / VFX / shaders | `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A3/B6/B7 → `assets/shaders/outline_glow.gdshader` → `scripts/Combat/GlowPresentationController.cs`, `VfxEmitter`, `ParticleBudget` → `scenes/vfx/`. `gl_compatibility` forbids `instance uniform`s and `TEXTURE` as a `sampler2D` argument |
| Input remapping / settings | `docs/design-contracts/COMFORT_SETTINGS.md` (C01c) + `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 A4 → `scripts/Core/InputBindings.cs` (`UnboundActions`, `ShortcutEnabled`, `InputShortcuts`) → `scripts/UI/SettingsMenu.cs` + `scenes/ui/Settings.tscn` → `scripts/Core/SaveManager*` (schema v7). **There is no chord editor** — recipes are fixed, only their component actions are remappable |
| Enemy or boss content | `docs/PACKAGE4_ROSTER_PLAN.md` §4.2 (+ its V7.6 addendum) → `resources/Enemies/`, `resources/Bosses/` → `scripts/Enemies/` → `resources/Content/content_manifest.csv` + `localization/en.csv`. Check `VERIFY-BOSS-HP` in the ledger before touching any boss `MaxHP` |
| Narrative / dialogue copy | `design-godot.md` Section 16 → `resources/Dialogue/` + `localization/en.csv`. Hero variants are `<baseID>@<heroID>`; the V7.5 vocabulary and the Act III knowledge boundary are gated by `NarrativeKnowledgeBoundaryTests`. The antagonists are **the Severed** and their agents **the Linked** (S12/S14, English values only — keys and IDs keep "unbound"); render-time tokens live in `DialogueTokens` (`{HeroAcceptLine}`, `{HeroFarewellLine}`, …) and `MissingLegendTokens` (`{MissingSoFar}`, `{MissingPlaces}`) |
| Collision changes | `project.godot` layer names + `CollisionLayers` constants + scene masks + tests, all together |
