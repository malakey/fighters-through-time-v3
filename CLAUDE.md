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

### Failure signature: a silently partial test run

If `GodotSharp\` goes missing or the Godot binary is moved away from it, every Godot launch aborts with:

```text
ERROR: .NET: Assemblies not found
   at: initialize (modules/mono/mono_gd/gd_mono.cpp:650)
CrashHandlerException: Program crashed with signal 11
```

The dangerous part is what `dotnet test` does next: GdUnit4 logs `Rebuilding Godot Project ends with exit code: -1073741819`, silently skips every engine-dependent test, runs only the ~20 pure-C# ones, and **still exits 0 reporting `Passed!`**. Always read the `Total:` count against the baseline below — a green exit code alone is not evidence the suite ran.

## Verified commands

Build (verified: succeeds with 1 pre-existing vendored warning — `CS8632` in `addons/gdunit4/src/dotnet/GdUnit4CSharpApi.cs`, not caused by your change. The old `CS9057` analyzer-version warning disappeared with the move to SDK 10):

```bash
dotnet build FightersThroughTime.csproj --nologo
```

Full headless test suite (GdUnit4 spawns Godot itself; `.runsettings` forces serial headless execution). Verified 2026-08-07 on `net10.0`: **352 passed, 0 failed, Total 352** in a few seconds after a warm build. Read the `Total:` count in the summary, not just the exit code — see the failure signature above:

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

Useful scene targets: `res://scenes/menus/MainMenu.tscn` (main scene), `res://scenes/arenas/TestArena.tscn` (Fighter sandbox), `res://scenes/fighter/FighterStage_Florence.tscn` (production stage), `res://scenes/diagnostics/PerformanceBaselineRunner.tscn` (performance baseline runner).

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
| UI / dialogue | `scenes/ui/`, `scripts/UI/`, `resources/Dialogue/`, `localization/en.csv` |
| Collision changes | `project.godot` layer names + `CollisionLayers` constants + scene masks + tests, all together |
