# Feel and UI update (2026-10-25): Echo Step removal, centred menus, encounter areas without the shift or the blue line, Fighter finisher / wall bounce / hitstun shove, block-success effect, UI redesign mockups

Status: **in progress** (orchestrated 2026-10-25). Base: `0eba848`, test baseline **3529**.

This is a scaled-down package plan: one worktree agent per concern, locked decisions in each brief, an orchestrator who merges, and a serial closeout that writes the shared docs. Workstream IDs are `FU-*`; handoffs are `docs/handoffs/FU_<WS>.md`.

## 0. Sources

The user's request of 2026-10-25, seven items:

1. "Remove echo step — it doesn't seem to do much and I think its just adding extra complexity to the game. If you have different thoughts about if we can improve it instead let me know."
2. "Some menus appear off center and fall off the screen. Look into this and find a fix so that all menus appear centered on the screen."
3. "When moving into a 'room' in the story mode the player/screen is shifted to the right quickly and there is a blue line showing the edge. Fix the shift and remove the blue line. Also remove the 'Enemies left' count and the GO that appears."
4. "In fighting mode I want to increase the knockback effect from the final combo hit so the enemy is knocked back further. I also want there to be wall bounce effects if the enemy gets hit into a wall."
5. "In fighting mode when an enemy is hit by an attack the hit stun does a strange slide. The enemy should be more fixed in place and just hit back a bit, try to remove the sliding that is occuring."
6. "When a block is successful I want there to be some visual animation that occurs that gives a clear indicator that the block was successful."
7. "I also want to do a redesign of the menus and UI HUD to give a better look that matches the design of our game. Create some mockups for this and give me options to select for the different design elements for the UI."

### 0.1 Rulings received (2026-10-25, one AskUserQuestion round)

| # | Question | Ruling |
|---|---|---|
| R1 | Fighter finisher distance | **2× the distance** (chosen over the 1.5× recommendation). Story's finisher is unchanged. |
| R2 | Fighter wall bounce | **Bounce, no damage**: rebound at about half speed, an impact burst, a short freeze and camera shake, once per launch. |
| R3 | Fighter hitstun slide | **Short shove, scaled**: strong friction stops a grounded victim within about 6–8 frames, then it is planted for the rest of the stun; light hits move about 0.2 units, heavier ones about 0.5. |
| R4 | Encounter areas without the blue line | **Invisible walls**: the area still holds the hero but nothing is drawn; the camera only moves when the hero moves and tightens onto the area as the hero walks in. |

**Echo Step.** The user invited a counter-proposal. Ours: remove it as asked. It is unbound by default and reachable only through a chord, it overlaps the roll, the air dodge, Time Freeze and the death rewind, it spends meter the Ultimate wants, and its 31-sample ring is the largest block in the Fighter rollback snapshot. The one variant worth reconsidering later is a meter-priced combo breaker, which would fill a gap Echo Step never did.

### 0.2 Recon findings (2026-10-25, read from code; one headless probe)

- **Menus.** Four code-built panels call `SetAnchorsPreset(LayoutPreset.Center)` without offsets, so their top-left corner sits on the screen centre and they grow right and down: `HolodeckConsolePanel` (`scripts/UI/HolodeckConsolePanel.cs:94-95`; the hub Holodeck and the Versus CPU stage phase; measured rect (960, 540, 640 × 629), bottom edge at y 1169, off-screen), `LocalFighterPause` (`LocalFighterPause.cs:176-177`; off-centre by (184, 182)), `NetworkSelectScreen` (`:28`; off-centre; LAN is de-scoped) and `TimelineRestartPanel` (`:50-52`; centred only by a hard-coded `Position`). No UI lives in world space (no `follow_viewport`; the 1.5 Story zoom is not involved). Tall panels are at risk at the maximum UI scale 1.4 (`GlobalSaveData.MaxUiScale`): the MainMenu root panel is 886 px tall at 1.0, the Resonance grid 867, the Holodeck console 629. `FirstUseTooltipPresenter.cs:62` uses a fixed `Position (560, 150)`.
- **Encounter locks.** `EncounterLock.Engage` (`EncounterLock.cs:391-406`) calls `StoryCameraConfiner.SetLockBounds(CameraBounds)`, which eases the Camera2D limits over `EncounterLockRules.CameraEaseSeconds` (0.6) toward the lock's camera bounds (the area grown to the 1280 × 720 view). The hero engages about 44 px inside the area's edge, so the view's edge starts about 600 px outside and the confine drags the camera about 600 px in 0.6 s. The blue line is `EncounterLockBarrier._Draw` (`EncounterLockBarrier.cs:138-172`), drawn while closed. Plain room `SetBounds` snaps but is authored so the old view never straddles a room edge, so it is not the cause. The HUD's "Enemies left" and GO are `scripts/UI/StoryHUD.EncounterLock.cs` + `scenes/ui/StoryHUD.tscn` `SafeArea/EncounterLockPanel` (lines 534-571), keys `hud_encounter_enemies_left` / `hud_encounter_go`; the HUD is the only non-test subscriber of `EventBus.OnEncounterLockChanged`.
- **Block feedback.** A Story ordinary block shows the `block` pose, a 3 px / 0.08 s shake, a haptic buzz and a 2-frame hitstop; a perfect block adds a 0.12 s cyan-white sprite tint; a shatter shows the daze pose and a heavy buzz. Fighter mirrors that and detects a block by a block-charge drop, the perfect-block count rise (323 `BlockTiming` bits 20–23) and a daze edge (`FighterSimulationDriver.cs` ~837-922).
- **Fighter knockback** (code reading plus a Python port of the tick order; not checked against a run). The finisher's force is `BasicKnockback × profile.FinisherKnockbackTenths / 10` (`FighterSimulationSystems.cs` ~2521; 45 tenths, Mozart 55) in **both** modes. `BasicComboRules.KnockbackMultipliers[2]` (4.5) is not read for the finisher at runtime, so a Fighter-only change must be a new sim-local constant, never an edit of the profile tenths or the table. The force is `ScaleByMissingHP(kb) / (1 + weight)` (`FighterEntitySystems.cs` ~931); the finisher launches at 45° (`vx = vy = force`), and DI rotates the stashed launch ±15° at hitstop end. **The slide:** in hitstun or daze the movement branch (`FighterSimulationSystems.cs` ~623-628) only steps `Velocity.y`; `Velocity.x` is never touched, so a grounded victim glides at constant speed for the whole hitstun (hit 1 30 frames, hit 2 40) and then stops dead on the 4-frame run-stop ramp. Modelled glide: hit 1 0.50 u (Cleopatra, 1.04 u/s) to 1.28 u (Lincoln, 2.6 u/s); hit 2 0.85–2.17 u. The guard-break daze push (±2.0, −1.0 for 60 frames) shares that branch and must keep its push. **Walls:** position is clamped to ±wall (`~483`, `~807`) and `Velocity.x` is left pointing into the wall; walls are ±9 (Florence, Orléans, Paris, Alexandria, Globe, Nassau), ±10 (Chicago, Berlin, Gettysburg), ±8 (Vesuvius); spawn ±4; ceiling 9. **Finisher travel today** (victim weight 1.0, full HP, no DI): Cleopatra 1.27 u, Leonardo/Tesla 2.06, Einstein/Shakespeare/Tubman 2.84, Mozart 2.90, Joan 3.52, Lincoln 5.52; at 50 % HP about 1.6–2.2× those. Component 323 `ActionFlags` bits 4–31 are free (verified). The Story hero has the same constant-speed glide in `PlayerController.ProcessStunned` (not changed by this package).
- **Echo Step.** State lives in components 313–317 (524 B per fighter) and four 310 `FighterVerbComponent` fields (24 B): about 1096 B off every snapshot. `BasicComboRules.SelectRecoveryVerb` / `RecoveryVerb` are called only from the two Echo Step start paths, and the "Echo Step not legal" fallback already is all of the non-Echo behaviour, so the chord table simply goes. No level, puzzle, tutorial step or Level 0 lesson needs it; the Calibration Drill is the only lesson. Removing the CPU's Echo decision shifts the Medium/Hard RNG stream. Tests: −27 cases (3529 → 3502 for this workstream alone).
- Full dossiers: `docs/recon-2026-10-25/` (A Echo Step, B menus, C encounter-lock camera and HUD, D Fighter knockback, E block feedback).

## 1. Standing decisions (every workstream)

1. **Docs.** Implementers do not edit `AGENTS.md`, `CLAUDE.md`, `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`, `design-godot.md` or its master, the contracts or their docs-3 copies, or either `IMPLEMENTATION_STATUS.md`. Each writes `docs/handoffs/FU_<WS>.md`: what shipped with exact API names, the exact `[TestCase]` delta with arithmetic, every provisional number, what did not ship, and **drafted text** for each doc change (AGENTS.md bullets to replace, ledger rows, GDD lines). The closeout applies them.
2. **Mode scope.** FU-KNOCK is Fighter only: Story's 4.5× finisher, its D6a finisher carry and its D9 mob wall bounce are untouched, and nothing under `scripts/FighterSim/` reads a Story rulebook. FU-LOCK is Story only. FU-BLOCK is presentation in both modes. FU-ECHO removes the verb from both modes.
3. **Determinism.** `scripts/FighterSim/` stays fixed-point (`FP64`); a `KLOTHO_DET002` warning is a bug. Presentation never writes sim state.
4. **One canonical value.** Every new number lives once, as a named constant in one rulebook.
5. **No new Klotho component ID.** FU-KNOCK may use the reserved bits of component 323's `ActionFlags` (bits 4–31) and documents each one. FU-ECHO retires 313–317, which are never reused.
6. **No save schema bump** (stays v8). Old saves that name `gameplay_echo_step` must still load.
7. **Protocol stays v4.** `GameplayButtons` bit 12 becomes reserved, as the Dash bit 11 did. LAN is de-scoped, and every Fighter hash moves anyway.
8. **Tests.** One `[TestSuite]` per file; `[RequireGodotRuntime]` on any suite that touches the engine; the repository failure-message idiom (never an empty `OverrideFailureMessage`); restore `GameManager.CurrentSession` and free grouped nodes in `finally`. Report the exact `[TestCase]` delta.
9. **GdUnit and Godot launches are serialized across every checkout** (CLAUDE.md signature 5). Before any Godot launch (test run, `--import`, smoke run) acquire `D:/Projects/.ftt-gdunit-lock` with `mkdir` (atomic). If it exists, retry every 15 s; if it is older than 20 minutes and no `testhost` or `*_console` Godot process runs, remove it. `rmdir` it right after the run, even on failure. Drain `testhost` first. **Never kill a non-console Godot process**: the user has the editor open on the main checkout.
10. **Worktrees.** `D:/Projects/ftt-wt/fu-<ws>` on branch `fu/<ws>`, cut from this plan's commit, with `.godot/` copied from the main checkout to avoid a cold import. Never commit `.godot/`, `localization/en.en.translation` (the orchestrator regenerates it once after merging) or `*.png.import` line-ending churn (`git checkout -- assets`).
11. **Commit and report.** Commit on your branch; do not merge to `main` and do not push. Report the branch head SHA, the files changed, the test delta with arithmetic, the full-suite result, and the risks.
12. **Shared files.** See §3. Prefer new partial files with one-line hooks in a shared file.
13. **Localization.** Delete any key you orphan in the same change (`UnusedTranslationKeyTests`); new visible copy needs a key. Run `--headless --import` locally for your tests but do not commit the compiled translation.
14. **Provisional numbers.** Every agent-chosen number is recorded as provisional in the handoff.
15. **Nobody plays the build.** Say so; claim nothing beyond tests and probes.

## 2. Workstreams

### 2.1 FU-MENU — every menu centred and on screen (both modes)

- Wrap the four broken panels in a full-rect `CenterContainer` (the `ConfirmModal` / `StoryPause` / `MoveList` pattern) and drop `TimelineRestartPanel`'s hard-coded `Position`. `FirstUseTooltipPresenter` anchors its panel top-centre with grow both instead of a fixed position.
- Measure every menu surface at 1920 × 1080 at UI scale 1.0 and at the maximum 1.4. Fix any overflow with one shared approach (agent's choice, documented): a reusable fit-to-safe-area container or helper that keeps a centred panel inside the screen, or `ScrollContainer`s with focus following for list content. Do not rename authored node paths unless unavoidable; if you must, update every reference.
- Regression tests: a `MenuLayoutBoundsTests` sweep of the authored menu scenes and the code-built panels at both scales, asserting each visible main panel lies inside the viewport and is centred where intended (explicit allow-list for deliberately off-centre layouts such as the MainMenu root screen); plus a source scan that fails on `SetAnchorsPreset(LayoutPreset.Center…)` without offsets.
- Out of scope: the visual redesign (§2.6).

### 2.2 FU-LOCK — encounter areas: no shift, no blue line, no counter or GO (Story)

- **Camera: no forced pan.** Replace the lock-bounds ease with a ratchet. On `SetLockBounds(target)`, and on `ClearLockBounds()` (whose target is the room bounds), the working limits become the union of the current visible rect and the target; a side that the target widens opens at once. Each physics step, after the follow rig computes its unclamped desired view, each side of the working limits moves toward the target only when the desired view has moved past it, inward only and never back. So the camera only moves when the hero does, and the view tightens onto the area as the hero walks in. Room `SetBounds` stays swallowed while a lock holds; `SnapToTarget` and rewind landings fold into the ratchet state. Remove `CameraEaseSeconds` and the ease code if nothing else reads them.
- **Barriers: invisible.** Keep the collision, `IsClosed`, the closing push and every rule; draw nothing (delete the drawing code and its visibility toggling).
- **HUD.** Delete `SafeArea/EncounterLockPanel` from `StoryHUD.tscn`, `scripts/UI/StoryHUD.EncounterLock.cs`, its four hooks in `StoryHUD.cs`, the keys `hud_encounter_enemies_left` and `hud_encounter_go`, and `EncounterLockRules.GoPromptSeconds` / `GoGlyph` if nothing else reads them. Keep `EventBus.OnEncounterLockChanged` and its payload, and keep the authored `GoDirection` data (documented as unread by presentation). The objective line (`encounter_lock_objective_clear`) stays.
- Out of scope: lock rules, members, waves, checkpoints, the bots.

### 2.3 FU-BLOCK — a clear block-success effect (both modes, presentation only)

- A new pooled, code-drawn `BlockSparkEffect` (`Node2D` drawing with `_Draw()` primitives, the `TelegraphGlyph` precedent) that registers its own pool the way `FloatingDamageNumber.Show` does, so no pool config needs a row. Three kinds, told apart by **shape** as well as colour:
  - **Ordinary** — a cyan-white shield arc across the blocker's front, a brief facet flash and 5–7 spark streaks; about 0.28 s.
  - **Perfect** — a gold-white double-ring burst plus the arc and more sparks; about 0.4 s. The existing 0.12 s tint stays.
  - **Shatter** — red-orange shards flying out and a cracked ring; about 0.45 s.
- Placed at the blocker's front at chest height. Bright fills follow `ComfortSettings.FullScreenFlashAllowed`; nothing strobes. No text, so no localization.
- Hooks: Story — the `PlayerController` block branch (ordinary, perfect, shatter) and the hazard block path (`PlayerController.EnvironmentalBlock.cs`); Fighter — `FighterSimulationDriver`'s block-charge-drop branch (ordinary), `SyncPerfectBlockFlash` (perfect) and the daze edge (shatter). Prefer new partials with one-line hooks.

### 2.4 FU-KNOCK — Fighter finisher ×2 distance, wall bounce, hitstun shove (Fighter only)

- **Finisher (R1): a horizontal-only boost.** The Fighter finisher launches with `vx = 2 × force` and `vy = force` (today both are `force`), through one Fighter-only named constant applied where the sim builds the finisher's intent (`FighterSimulationSystems.cs` ~2521). Keeping `vy` keeps the arc height and airtime, so the travel doubles. Scaling both axes would lengthen the airtime past the 24-frame launch hitstun, where post-hitstun air control strips the extra speed (modelled: ×1.5 on both axes gave only ×1.7 travel). The missing-HP ramp, the weight term and DI still apply. `FinisherKnockbackTenths` and `KnockbackMultipliers` stay untouched, so Story does not move. Expect Lincoln's full-HP finisher (5.5 u today) to reach a wall from almost anywhere; the user ruled 2× for every kit, so no per-kit cap. Report every kit's measured travel before and after.
- **Wall bounce (R2).** Hook at the side-wall clamp in the movement system (`FighterSimulationSystems.cs` ~806-807): if the position was clamped this tick, the fighter is launched (310 `Tumble` ≠ 0, which covers the launch hitstun and the post-hitstun tumble), its `vx` points into the wall at ≥ 5 u/s (provisional; Story D9's 300 px/s) and it has not bounced this launch, then `vx = −0.5 × vx` (provisional), the victim takes 6 frames of hitstop (provisional) and a once-per-launch flag is set in 323 `ActionFlags` bit 4, cleared wherever a new launch is applied (beside `StashPendingLaunch`). A wrapping bounce counter in 323 `ActionFlags` bits 8–11 (mirroring the perfect-block count) lets the driver edge-detect a bounce read-only and play an impact burst at the wall contact point plus a camera shake through `CameraShake` (so the accessibility scale applies); a haptic is optional. No new core cue (FU-ECHO edits that catalog, and every stream is silent anyway). No damage, no hitstun change, no meter. Grounded slides never bounce (they are not launches). The knockdown roll's own clamp is untouched.
- **Hitstun shove (R3).** In the hitstun branch (`FighterSimulationSystems.cs` ~623-628), guarded by grounded, `DazeFrames <= 0` (the guard-break push keeps its frictionless push) and `Tumble == 0` (launches keep their flight): replace the constant-speed glide with a quick shove. Targets: the weakest string hit (Cleopatra hit 1 at full HP, 1.04 u/s today) moves the victim about 0.2 u, the strongest grounded non-launcher (Lincoln hit 1, 2.6 u/s; Joan hit 2, 2.33 u/s) about 0.5–0.6 u, most of it within 6–8 frames and fully stopped by about 15, then planted for the rest of the stun. Suggested shape (agent may choose another that meets the targets): scale the grounded non-launching horizontal speed by a constant at the hit site and apply a per-tick geometric decay with a snap to zero below a small epsilon, so the distance stays proportional to the hit's strength (about ×3 and ×0.79 per tick meets both targets in the model). Fixed-point only. Hazard knockback takes the same path and gets the same shove.
- Every Fighter hash moves; update pins and say why.

### 2.5 FU-ECHO — remove Echo Step (both modes)

- Remove the verb entirely, following `docs/recon-2026-10-25/A_echo_step_removal_map.md`: the Fighter sim (components 313–317 retired and never reused, the four 310 fields, `FighterEchoStepRules.cs`, sampling, wind-up, teleport, spawn / stock-loss / Sudden Death resets, `FighterActionKind.EchoStep` deleted with ordinal 10 left as a gap), the CPU (`CpuVerbAction.EchoStep`, the admission state, `CpuBandTuning`'s three Echo fields and anything that becomes dead), the Story controller and its 10.5 cost (the Mirror clone follows), `BasicComboRules`' five Echo constants and the whole `RecoveryVerb` / `SelectRecoveryVerb` block, the `gameplay_echo_step` action, its C01c chord recipe (two recipes remain: the Ultimate and the Grab), `GameplayButtons.EchoStep` (bit 12 becomes a reserved comment like Dash's bit 11), the Fighter HUD's fourth radial (P1 and the mirrored P2 row), the Calibration Drill (six become five; delete the two-beat objective machinery Echo was the only user of), the three core cues, and the eight translation keys.
- **The chord afterwards:** none. Block + Roll does what its inputs already do (a roll from the stance, the recovery cancels, the air dodge in the air, nothing on a ledge hang — `FighterLedgeRules` keeps its `!blockHeld` rule). Drop the D3 Roll exclusions in both modes together (`FighterSpecialCancel.cs` and Story's `BufferSpecialCancelPress`). `DirectOrigin` becomes "the grab chord is off".
- **Systems Card:** rename the `echo` section to `momentum` (nodes, keys, the content test) and keep only its per-hit Momentum sentence under a "Momentum" title.
- **Saves:** schema stays v8. Stale `gameplay_echo_step` binding rows load and are ignored (restore is already filtered by the InputMap). `StoryPlayerResourceTimers.EchoStepCooldownFrames` stays as documented dead data (the `RatingByLevel` precedent); only its capture and restore go.
- Protocol v4; every Fighter hash moves; seeded Medium/Hard CPU behaviour shifts.

### 2.6 FU-MOCK — UI and HUD redesign mockups (outside the repo)

Self-contained HTML mockups and an `OPTIONS.md` under `D:/Projects/fighters-through-time-docs-2/mockups/ui_redesign_2026-10-25/`: three or four visual directions (main menu, character select, pause, Story HUD, Fighter HUD each) and a per-element option matrix. The user then picks per element in decision rounds. Implementing the chosen redesign is a later package.

## 3. File ownership

| File or area | Owner | Others may |
|---|---|---|
| `project.godot` | FU-ECHO | nobody else edits it |
| `scenes/ui/StoryHUD.tscn`, `scripts/UI/StoryHUD*.cs` | FU-LOCK | — |
| `scenes/ui/FighterHUD.tscn`, `scripts/UI/FighterHUD*.cs` | FU-ECHO | — |
| `scripts/Environment/EncounterLock*.cs`, `StoryCameraConfiner*.cs`, `StoryCameraRules.cs` | FU-LOCK | — |
| `scripts/FighterSim/FighterSimulationDriver*.cs` | shared | FU-BLOCK and FU-KNOCK add new partials plus one-line hooks; FU-ECHO deletes Echo code |
| `scripts/FighterSim/` movement, damage, air physics, rules | FU-KNOCK | FU-ECHO deletes Echo code |
| `scripts/Combat/BasicComboRules.cs` | shared | FU-KNOCK adds a Fighter section; FU-ECHO deletes Echo members |
| `scripts/Characters/PlayerController*.cs` | shared | FU-BLOCK adds a partial plus hooks; FU-ECHO deletes Echo code |
| Menu scenes and scripts (`scenes/menus/`, `scenes/ui/` except the HUDs, `scripts/UI/`) | FU-MENU | FU-ECHO edits only the Echo rows of the Move List, Systems Card, Settings and Calibration Drill surfaces; FU-MENU restructures those four only if its sweep finds them broken, and says so in its handoff |
| `scripts/Core/CoreMechanicCueCatalog.cs`, `resources/Audio/core_mechanic_cues.tres`, `scripts/Core/Input*.cs`, `scripts/Networking/`, `PlayerInputFrame.cs`, Calibration Drill code | FU-ECHO | — (no other workstream adds a cue) |
| `localization/en.csv` | shared | FU-LOCK and FU-ECHO delete rows; FU-MENU and FU-BLOCK add none unless unavoidable |
| `localization/en.en.translation` | orchestrator | regenerated once after the merges |
| `tests/**` | each owns its new files | edits to shared test files stay minimal and are listed in the handoff |

## 4. Merge order and validation

- Per workstream: `dotnet build` (0 errors, no new warnings), the affected suites with `--filter`, and one full suite run at the end, all under the lock; the handoff states the exact expected total.
- Merge order: FU-MENU → FU-BLOCK → FU-LOCK → FU-KNOCK → FU-ECHO (the most invasive last). After the merges: regenerate `en.en.translation` with `--headless --import`, build, then three consecutive full-suite runs whose `Total:` equals 3529 plus the sum of the declared deltas.
- Review: the integrated change goes to the cross-provider review before the closeout commits.
- Closeout (one serial agent in the main checkout): AGENTS.md, CLAUDE.md (baseline, Echo Step mentions), the ledger section "Confirmed open deviations — feel and UI update, 2026-10-25", the GDD master and mirror (mind `DOC-MASTER-UNBOUND-BINDING`), the contracts and their docs-3 copies, §9 and §10 here; then prune the `fu-*` worktrees and merged branches.

## 9. Deviation log (append-only)

(empty)

## 10. Closeout

(pending)
