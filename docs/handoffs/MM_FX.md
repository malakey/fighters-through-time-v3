# MM-FX handoff (meter and Defy update, Phase B1): the Defy presentation

Branch `mm/fx` (worktree `D:/Projects/ftt-wt/mm-fx`), cut from `9cba2b9a` (`mm-rules`).

## 1. Shipped (exact names)

- `FTT.Combat.DefyBurstEffect` (`scripts/Combat/DefyBurstEffect.cs`, scene `scenes/vfx/DefyBurstEffect.tscn`): pooled, code-drawn gold ring. `Show(Vector2 worldPosition, Node parent, float radiusPixels = StoryRadiusPixels)`, `Seconds` 0.4, `StoryRadiusPixels` 150, `FighterRadiusPixels` 156.25, `IsActive`, `Radius`, `Age`, `FillsAllowed`. The inner fill and the white core follow `ComfortSettings.FullScreenFlashAllowed`.
- `FTT.UI.DefyScreenPulse` (`scripts/UI/DefyScreenPulse.cs`): `CanvasLayer` 60 plus a full-rect `ColorRect` on `ui_desaturate.gdshader` (saturation 0), 0.25 s, peak alpha 0.30, frees itself. `Show(Node parent)` returns null under Reduced Temporal Effects (`ScreenTintPulseAllowed`).
- `FTT.UI.FloatingDamageNumber.ShowText(string text, Vector2 position, Node parent = null, Color? tint = null, float scale = 1f)` and the `DisplayedText` test seam. Shows regardless of `DamageNumbersVisible`.
- `FTT.Combat.DefyPresentation.Play(Vector2 center, Node parent, float radiusPixels)` (new, `scripts/Combat/DefyPresentation.cs`): ring, `defy_world_label` text in `UIPalette.GoldBright` at 1.25x, pulse, `CameraShake` 3 / 0.1 s, then `CoreCues.PlayDefyBurst()`. Constants `LabelKey`, `LabelScale`, `LabelRisePixels` 60.
- `CoreMechanicCuePresenter`: subscribes to `EventBus.OnDefyExecuted` (`OnDefyExecuted(DefyExecutedPayload)`, draws into `GetTree().CurrentScene`), `PlayDefyBurst()` (the Critical cue plus `AudioSnapshot.DefyOrScripted` for `DefySnapshotSeconds` 0.25, released in `Advance`), `DefySnapshotHeld`. The `OnDefySealChanged` subscription and `_defyStates` are gone.
- `CoreMechanicCueIDs.DefyBurst = "defy_burst"` (Critical, in `All` and `Critical`); `DefyToll` and `DefySealShatter` removed from the code, the two rows replaced by `defy_burst` in `core_mechanic_cues.tres` (silent placeholder stream, same one the old rows used).
- `PlayerController.DefyFeedback.cs` (Story): `PresentDefyStart()` holds the `ultimate` sheet's first key pose and paints the spawn-invulnerability aura; `PresentDefyBlast(in DefyExecutedPayload)` ends the aura (unless post-rewind protection runs) and plays `idle` (P30). `DefyGlowActive` test seam.
- `FighterSimulationDriver.DefyFeedback.cs` (Fighter): `PresentDefyCounter(PlayerController, int playerID, in FighterActionStateComponent)` (public, for tests), `SyncDefyFeedback`, `IsDefyInvulnerable(in action)` (DefyFrames 1..`DefyInvulnerableFrames`), `DefyBurstRisePixels` 60. Fires once on a short forward step of the 4-bit count (`IsForwardCounterStep`, ring 16) with `DefyFrames > 0`; the first read only records a baseline.
- `localization/en.csv`: `# MM-FX block`, `defy_world_label,DEFIED`.

## 2. Decisions

- The Fighter ring fires on the counter step, which MM-SIM steps at the Defy's first frame (Begin), not at the blast (frame 6). Plan section 2.11 names both the counter and the blast-fired bit; the brief's test wording ("a counter forward step spawns one ring") fixed it to the counter. The ring therefore starts 6 frames (0.1 s of its 0.4 s) before the blast fires; Story's fires on `OnDefyExecuted`, i.e. at the blast. If the user wants the Fighter ring on the blast, the driver can instead edge-detect `ActionFlagDefyBlastFired` (one line). Recorded as `PROVISIONAL-DEFY-PRESENTATION`.
- The screen pulse reuses `ui_desaturate` literally on a flat translucent grey `ColorRect`: a canvas_item shader cannot sample the screen without a `hint_screen_texture` uniform, so this reads as a pale grey wash, not a true desaturation of the frame. A real screen-texture shader is a follow-up if the look is wanted.
- The Story aura ends at the blast hook (frame 6, the last invulnerable frame); the Fighter aura is `IsDefyInvulnerable` ORed into the existing `SetSpawnInvulnerability` push, so "Respawning" (which reads `ShowsSpawnProtection`) is unaffected.
- No change to `GlowStateModel` / `GlowPresentationController`: the documented `SpawnInvulnerability` layer already says "post-Defy".
- "Replaces any damage number on the triggering frame": a Defy raises no hit, so no damage number exists on that frame; nothing to replace.
- `OnDefySealChanged` / `DefySealPayload` stay on `EventBus` for MM-SWEEP; only my listener went (two-stage rule). `FirstUseTooltipPresenter`'s seal listener is MM-TEXT's.

## 3. Deviations from the plan
None beyond section 2's Fighter-ring timing choice.

## 4. Cross-edits (files I do not own)
- `FighterSimulationDriver.cs`: the three granted spots: one call line `SyncDefyFeedback(player, playerID)` in the presentation sync, `|| IsDefyInvulnerable(in actionState)` on the glow's invulnerable flag, and the Defy cases in `GetStateLabel` ("Defying") and `ResolvePresentationAnimation` (`ultimate` pose 0 through frames 1-6, `idle` through recovery).

## 5. Tests
- New: `DefyBurstEffectTests` 2 (`TheRingLivesItsDurationThenReturnsToThePoolAndAReusedOneStartsClean`, `TheBrightFillsFollowFullScreenFlashAllowed`); `DefyPresentationTests` 4 (`OneStoryDefyEventShowsTheRingAndDefiedEvenWithDamageNumbersOffAndRequestsTheCueAndSnapshot`, `TheScreenPulseIsSkippedUnderReducedTemporalEffectsButTheRingStillPlays`, `AFighterDefyCounterForwardStepSpawnsOneRingAndAnUnchangedOrLoweredCounterNone`, `TheDefyInvulnerableFramesAreOneToSixAndTheWorldLabelResolves`); `CoreMechanicCueCatalogTests` +1 (`TheDefyBurstIsCriticalAndTheRetiredSealCuesAreGone`). Net **+7**, as planned.
- In place: `CoreMechanicCueCatalogTests.StoryEventsDriveTheirCuesFromTheExistingBus` (the Defy block now raises `OnDefyExecuted` and expects `defy_burst` on the clear path); the H04 list case follows `CoreMechanicCueIDs.All`.
- Seen failing (mutations, each reverted): damage-numbers gate added to `ShowText` -> the Story case fails; pulse gate removed -> the pulse case fails; `_flashAllowed = true` -> the fills case fails; `ApplySnapshot` removed -> the Story case fails; forward-step check removed -> the Fighter counter case fails after its lowered row was changed to a Defy-running lower (`At(0, 5)`; with `DefyFrames` 0 the `DefyFrames` guard alone caught it, so that row would not have shown the mutation).
- Commands: `dotnet build FightersThroughTime.csproj --nologo` 0 errors; `--import` under the lock; `dotnet test --filter "DefyBurstEffect|DefyPresentation|CoreMechanicCue|BlockSparkEffect|GlowPresentationController|FighterPresentationSync|ScriptTranslationKey|TranslationCsvShape|UnusedTranslationKey|AudioSnapshot"`: `Failed: 0, Passed: 71, Total: 71`; the three FX suites alone 13/13. `TestArena.tscn --headless --fixed-fps 60 --quit-after 300` under the lock: exit 0, no script errors (a pre-existing `Invalid polygon data` from `PulpAbilitySlot.SweepLayer._Draw`, and the usual 3 resources in use at exit). The full suite was not run.

## 6. For the docs
- `USER-DEFY-TIME-BREAK-PRESENTATION` (4.1), `PROVISIONAL-DEFY-PRESENTATION` (O17: ring 0.4 s and radii, pulse 0.25 s at 0.30 alpha, label 1.25x, shake 3 / 0.1 s, snapshot 0.25 s, Fighter ring on the Defy's first frame), `PROVISIONAL-DEFY-POSE` (P30).
- `HUD_CONTRACT`/audio docs: `defy_toll` and `defy_seal_shatter` are retired; `defy_burst` is the one Defy cue.
- `localization/en.en.translation` was regenerated locally to run the tests and is not committed.

## 7. Open
Nobody played the build: not the ring's look at 1.5x zoom or on the Fighter camera, the label, the pulse, the aura, the pose, or the Fighter ring's timing against the blast.
