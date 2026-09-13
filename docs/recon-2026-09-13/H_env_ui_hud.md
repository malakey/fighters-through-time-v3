# Workstream H — Environment & UI / HUD gap dossier

Design source: `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md` §7 (master lines 3032–3462),
plus `docs/HUD_CONTRACT.md` (F24), `docs/COMFORT_SETTINGS.md` (C01a/b/c), `docs/DESIGN_BUILD_DEVIATIONS.md` (P04),
and the §7-referenced contracts `docs/NARRATIVE_RESOLUTION.md` (N01–N05) and `docs/MULTIPLAYER_DELIVERY.md` (M01).
Repo baseline: `D:\Projects\Fighters Through Time - V3` @ `31fed14` (V7.4 stun-lock pass, 2026-08-29). Read-only recon; nothing edited.

Diff read in full: `scratchpad/diff/H_env_ui_hud.patch` (13 hunks, master lines 3066–3458).

---

## 0. Headline findings (read these first)

1. **The authored Story HUD scene bears almost no relation to the HUD_CONTRACT hierarchy.**
   `scenes/ui/StoryHUD.tscn` is `CanvasLayer/Root/{TopLeft,Portrait,Vitals,BossPanel,CheckpointToast}` —
   a single left-hand `Vitals` VBox of `ProgressBar`s and `Label`s. There is **no `SafeArea`, no
   `TopLeft_Panel`, no `TopRight_Panel`**, and four HUD elements the design names as authored nodes
   (Integrity readout, Rally echo band, boss intro card, rewind-cooldown pip) are **built in code at
   runtime by `StoryHUD.ResolveUI()`** with comments explicitly saying "built in code so the authored
   scene stays untouched". V7.6 adds seven more named nodes on top. This is a **scene rewrite**, not an
   edit, and it is the single largest item in this workstream.

2. **Both status systems already carry two slots; only the HUD collapses them.**
   - Story: `scripts/Combat/StatusController.cs:113-128` has `_control` and `_damage` slots plus
     `IsDamageStatus(type)` (Venom, RadiantBurn → damage; the rest → control).
   - Fighter: `FighterRuntimeComponent` (`scripts/FighterSim/FighterSimulationComponents.cs:287-386`)
     bit-packs **both** slots — `StatusType`/`StatusFrames`/`StatusIntensity` (control) and
     `DamageStatusType`/`DamageStatusFrames`/`DamageStatusIntensity` (damage) — and exposes
     `PresentedStatusType` *purely as a single-pip collapse helper* ("Single-pip presentation: the
     control status leads").
   F24's two-slot HUD therefore needs **zero new simulation state and no Klotho component**
   (important: that struct is at exactly 128 bytes and cannot grow). It is a pure presentation change:
   stop calling `PresentedStatusType`, read both pairs. This removes what looks like the scariest
   risk in the F24 item.

3. **Time Freeze does not exist anywhere in the repo.** `grep -rn "TimeFreeze\|time_freeze" scripts/ project.godot`
   returns nothing. The HUD's `TimeFreezeIndicator` is blocked on the Time-Freeze workstream shipping the
   verb + action + 5 s/45 s clocks. What the repo *does* have in that slot is the retired V7.3 artifact:
   a code-built **manual-rewind cooldown pip** (`StoryHUD.RefreshRewindCooldownPip`, key
   `hud_rewind_cooldown`) reading `ChronalRewindManager.ManualRewindCooldownRemaining` — the 12 s manual
   rewind that F03 replaces. That pip must be **deleted**, not repurposed.

4. **Level 15 already ships the N01 sealing interaction, once.** `scripts/Environment/TemporalCoreAnchor.cs`
   is a player-Interact anchor that gates `ShowCompletionResults()` behind a deliberate press, with
   armed-state derivation and a rewind-safe design. N01 generalises exactly this to every boss level.
   Treat `TemporalCoreAnchor` as the pattern (it is explicitly documented as "bespoke to the finale
   rather than a shared toolkit component" — that comment is the thing that changes).

5. **Story bottomless pits (F16) have no implementation at all.** No kill boundary, no fall death.
   `PlayerController.cs:1968` even carries a forward-looking comment: *"(Should a true blast-zone/pit
   death path ever be added, it must bypass this — falling is not a hit.)"* next to the Defy History
   branch. F16 Option B says exactly that. The comment is a ready-made hook site.

6. **`ChronalRating` is retired by V7.6 but is wired through five layers**: `ChronalRatingRules.Compute`,
   `StoryManager.LastLevelChronalRating`, `save.RatingByLevel[levelID]`, `LevelResultsPanel._ratingLabel`,
   and `results_rating` in `en.csv`. Removal touches the save schema (or leaves a dead field), which
   makes it a coordination item with the Saves workstream.

---

## 1. Status summary table

| ID | Design item | Status | Size |
|---|---|---|---|
| H-01 | Story HUD node hierarchy per HUD_CONTRACT | Divergent (scene rewrite) | L |
| H-02 | Story two status slots + radial timers | Missing (data exists) | M |
| H-03 | Persistent top-right dust counter + pooled `+N` float | Divergent (top-left label, no float) | S–M |
| H-04 | Integrity era-clock face, wedge, cracks, siphon streams, seal | Divergent (code-built % label) | M–L |
| H-05 | Rewind counter icon/pulse/empty-at-zero; delete 12 s cooldown pip | Divergent | S |
| H-06 | Separate Time Freeze indicator (Ready/Active 5 s/Cooldown 45 s) | Missing; blocked on Time Freeze WS | S (after unblock) |
| H-07 | Ability slot lock states Dormant / Suppressed / Clear | Missing; blocked on Legacy Unlock + Suppression | M |
| H-08 | `DefySeal` four-state indicator, both modes | Missing (state exists both modes) | S |
| H-09 | `BeaconAnchors` + anchor pips (Act III only) | Missing; blocked on Act III WS | S (after unblock) |
| H-10 | `RallyEcho_Band` named node, 2.5 s drain, reclaim flash | Partial (code-built, no flash) | S |
| H-11 | Ultimate meter 40×40 radial + ready flash + LockOverlay | Divergent (280×8 bar) | S–M |
| H-12 | Cooldown slots 36×36 icon + radial + ready blink | Divergent (62×6 bars) | M |
| H-13 | Block pips refill glow / shatter crack flash | Partial (pips correct, no animation) | S |
| H-15 | Collapse Tremor presentation (<20% / <10%) | Missing | M (mostly env WS) |
| H-16 | Fighter two status slots (24×24, damage left / control right, P2 not reversed) | Missing (data exists) | M |
| H-17 | Fighter Stock Display: icons vs "Stocks lost: N" by mode (F21) | Missing (always pips) | S |
| H-18 | Fighter `DefySeal` beside meter, mirrored on P2 | Missing | S |
| H-20 | `00:10` timer pulse + chime; "Sudden Death — next death loses" instead of clock | Partial (stamp only) | S |
| H-21 | Fighter HUD node naming vs contract | Divergent | S (or record deviation) |
| H-22 | P1/P2 ownership outline as separate persistent shader layer (F24) | Missing; shared with VFX WS | M |
| H-23 | Calibration Drills: standalone main-menu route + picker + drill list + 6 drills | Missing | **XL** |
| H-24 | Move List Story "Dormant — restored after Level N" badges | Missing; blocked on Legacy Unlock | S |
| H-25 | Main menu options incl. Calibration Drills | Missing entry | S |
| H-26 | Fighter Play Options (Local Versus / Steam Remote Play Together) | Missing (Fighter → CSS directly) | M |
| H-27 | "Archive Time-Ship" → "Warden Time-Ship" copy rename | Missing | S |
| H-28 | N01 sealing anchor on every boss level + `AwaitingSeal` persistence | Missing (L15 precedent only) | **L** |
| H-29 | Results itemisation per F05 categories | Divergent (3 lines) | S |
| H-30 | Chronal Rating retired | Present, must be removed | S–M (save coupling) |
| H-31 | Results Integrity = PreBoss-frozen value + tier bonus applied | Divergent (live value) | S (deps) |
| H-32 | Act III (L13/L14) no hub return, Beacon deposit | Missing | M (shared w/ Act III WS) |
| H-33 | Two endings selected by 15-level Integrity average (N05) | Missing | M (shared w/ Narrative WS) |
| H-34 | Hold-to-skip *any* sequence | Divergent (gated on completed+seen) | S |
| H-36 | Illustrated-still plot cinematics under the dialogue box | Missing | L (content WS) |
| H-38 | F16 lethal Story kill boundaries | Missing | **L** |
| H-39 | V01b Reset Puzzle interaction | Missing | M |
| H-40 | V01c dedicated puzzle props / accepted actions | Missing | M |
| H-42 | F12 checkpoint roles by authored role, 4A Entry+PreBoss, Hard-middle scoping | Divergent (ID-suffix keyed) | M |
| H-43 | `checkpointIntegrity` saved; PreBoss freezes the gauge | Missing | S–M (Integrity WS) |
| H-44 | Extractor dust = F05 allocation | Divergent (`DustReward = 15` default) | S (economy WS) |
| H-45 | Budgeted dust drops replace "every enemy drops dust" | Divergent | S (economy WS) |
| H-48 | Reduced Temporal Effects toggle (C01a) | Missing | M |
| H-49 | C01c full direct-binding list incl. Echo Step / Time Freeze | Divergent | M (input WS) |

---

## 2. Story HUD — the contract vs the scene

### Required (HUD_CONTRACT + master 3293–3342)

```
HUD_CanvasLayer (CanvasLayer)
└── SafeArea (Control, Full Rect)
    ├── TopLeft_Panel
    │   ├── PlayerPortrait (TextureRect 64×64)
    │   ├── HealthBar_BG (TextureRect 200×20)
    │   │   ├── HealthBar_Fill
    │   │   └── RallyEcho_Band              (V7.6)
    │   ├── BlockCharges_Panel (HBox, ShieldIcon_1..3, 20×20)
    │   ├── StatusEffects_Panel (Control, fixed slot positions)   (F24)
    │   │   ├── DamageStatus_Indicator (28×28) → DamageStatus_DurationRadial
    │   │   └── ControlStatus_Indicator (28×28) → ControlStatus_DurationRadial
    │   ├── UltimateMeter_BG (40×40)
    │   │   ├── UltimateMeter_Fill (radial)
    │   │   └── UltimateMeter_LockOverlay   (V7.6)
    │   ├── DefySeal (20×20)                 (F13)
    │   ├── RewindCounter (HBox: RewindIcon 32×32, RewindCountText)
    │   ├── TimeFreezeIndicator (Control, 32×32 + remaining label)
    │   ├── BeaconAnchors (HBox: BeaconIcon 24×24, up to 3 AnchorPips)  (Act III only)
    │   ├── Cooldown1_Icon (36×36) → Cooldown1_Radial, Cooldown1_LockOverlay
    │   ├── Cooldown2_Icon (36×36) → Cooldown2_Radial, Cooldown2_LockOverlay
    │   └── Cooldown3_Icon (36×36) → Cooldown3_Radial, Cooldown3_LockOverlay
    └── TopRight_Panel (VBox)
        ├── CurrencyContainer (HBox: ChronalDustIcon, ChronalDustText, DustPickup_Float)
        └── IntegrityClock (Control 48×48)
            ├── ClockFace, ClockWedge, SiphonStreams, IntegrityText
```

### What exists (`scenes/ui/StoryHUD.tscn`, `scripts/UI/StoryHUD.cs`)

| Design node | Repo node / mechanism | Delta |
|---|---|---|
| `SafeArea` | `Root` (Control, full rect) | rename or record deviation |
| `TopLeft_Panel` | `Root/Portrait` + `Root/Vitals` (VBox at 92,80) | no single panel; split across two siblings |
| `PlayerPortrait` 64×64 | `Root/Portrait` TextureRect 64×64 (`StoryHUD.cs:487`) | **OK** |
| `HealthBar_BG` 200×20 | `Root/Vitals/HPBar` ProgressBar 280×16 + `HPText` label | size deviation only |
| `RallyEcho_Band` | **code-built** `ColorRect "EchoBand"` added to `_hpBar` (`StoryHUD.cs:496-507`), positioned each frame from `FighterHudModel.EchoBandFraction` via `PlayerController.EchoPool` | promote to authored node; add reclaim flash |
| `BlockCharges_Panel` + 3×20×20 | `Root/Vitals/BlockCharges` HBox, pips code-built 20×20 (`BlockPipSize = 20`), capacity = `MaxBlockCharges` + Resonance bonus, tracked on `EventBus.OnBlockChargesChanged` | **OK** (deliberate data-driven build); missing refill glow + shatter crack flash |
| `StatusEffects_Panel` (2 slots) | `Root/Vitals/StatusIndicator` — one **Label** showing `Tr(status_*)` tinted by `GlowPalette.Status(...)` | **Missing**: 2 fixed slots, icons, radials |
| `UltimateMeter_BG` 40×40 radial | `Root/Vitals/MeterBar` ProgressBar 280×8 | shape deviation; no LockOverlay, no ready flash |
| `DefySeal` | — | missing |
| `RewindCounter` icon + `×3` | `Root/Vitals/RewindLabel` text `hud_rewinds` ("Rewinds: {0}") + code-built `RewindCooldownPip` | no icon, no red pulse at 1, no empty-at-0 state; **pip must be deleted (F03)** |
| `TimeFreezeIndicator` | — | missing entirely |
| `BeaconAnchors` | — | missing entirely |
| `Cooldown1..3_Icon` 36×36 + radial + lock | `Root/Vitals/Abilities/{Special1,Special2,Movement,Ultimate}/{SlotName,SlotBar}` — 62-wide VBoxes with a 62×6 `ProgressBar` and a text label, driven by `HudAbilityIndicatorModel` | icons, radials, lock overlays, ready blink all missing. Note the repo puts **Ultimate in the same row** as a fourth slot rather than as the separate meter ring |
| `TopRight_Panel` | — | missing; dust lives in the left `Vitals` VBox |
| `CurrencyContainer` / `ChronalDustText` | `Root/Vitals/DustLabel` using key **`hub_carried_dust`** from `StoryManager.ChronalDustCollected` | wrong anchor corner; wrong key family; no icon |
| `DustPickup_Float` (+N, pooled, 1.0 s) | — | missing. `ChronalDustPickup.Collect()` is the single wallet-commit point and already raises `OnDustAwardCollected` — that is the float's trigger |
| `IntegrityClock` 48×48 | **code-built** `Label "IntegrityLabel"` appended next to `DustLabel` (`StoryHUD.cs:515-522`), text `hud_integrity` ("Integrity {0}%"), red when the value fell since last frame, cyan otherwise (`RefreshIntegrity`, :165-179) | no clock face, wedge, tier cracks, siphon streams, audible tick <20%, pause dim, or PreBoss seal flourish; the "draining" test is a frame-delta heuristic, not a live-Extractor query |
| — | `Root/TopLeft/{LevelTitle,Objective}`, `Root/BossPanel/{BossName,BossBar/PhaseNotches}`, `Root/CheckpointToast`, code-built `BossIntroCard` | **Repo extras not in the contract.** Keep; record as accepted deviations (P04) — the boss bar is specified elsewhere in §7 and the title/objective/toast are shipped behaviour. |

### Change list (H-01…H-13)

- Rewrite `scenes/ui/StoryHUD.tscn` to the contract tree, keeping the repo extras as named siblings.
- Fold the four code-built widgets (`EchoBand`, `IntegrityLabel`, `BossIntroCard`, `RewindCooldownPip`)
  out of `StoryHUD.ResolveUI()` — three become authored nodes, the fourth is deleted.
- `HudAbilityIndicatorModel` (`scripts/UI/HudAbilityIndicatorModel.cs`) currently models **one** status
  (`ActiveStatus`, `StatusRemaining`, `ClearStatus()`). It needs a two-slot shape keyed by
  `StatusController.IsDamageStatus(payload.Type)`. `EventBus.StatusEffectPayload`
  (`scripts/Core/EventBus.cs:124`) carries `Type`, `Duration`, `Intensity` but **not** a slot id —
  either derive the slot from `IsDamageStatus` in the HUD (cheapest, no event change) or add a slot
  field (touches every raiser). **Recommend deriving**; note `OnStatusEffectCleared` also carries the
  `Type`, so clears can be slot-routed the same way.
- Radial timers: no radial widget exists anywhere in the repo (all readiness is `ProgressBar`).
  Either a `TextureProgressBar` in radial fill mode or a small `_Draw()` control
  (the pattern exists: `scripts/UI/BossPhaseNotchOverlay.cs`, `scripts/Environment/SequenceGlyph.cs`).
- F24 refresh rule: "Radials follow authoritative simulation clocks, including pauses and Time Freeze;
  the HUD must not independently count down a frozen status." The current model **does** count down
  locally (`HudAbilityIndicatorModel.Tick`, documented as a "presentation fallback"). Under Time Freeze
  that becomes wrong — this needs an authoritative remaining-duration read from `StatusController`.

---

## 3. Fighter HUD

Required tree (master 3344–3383): `Fighter_CanvasLayer/SafeArea/{TimerText, P1_Status, P2_Status}` with
per player `Portrait, HP_Bar(+RallyEcho_Band), BlockCharges_Panel, StatusEffects_Panel(2×24×24),
StockLayout/StockDisplay, UltimateMeter, DefySeal`.

Repo (`scenes/ui/FighterHUD.tscn`, `scripts/UI/FighterHUD.cs`), attached by `FighterSimulationDriver`:

| Design | Repo path | Delta |
|---|---|---|
| `SafeArea` | `Root` | rename/deviation |
| `TimerText` (top-center) | `Root/MatchClock` Label, 46 px, anchors top-center, `fighter_hud_clock` = `{0}:{1}` | present; **no `00:10` red pulse, no 10 s chime**; design says 48 px |
| `P1_Status` / `P2_Status` | `Root/PlayerOne` / `Root/PlayerTwo` PanelContainers, bottom-anchored | rename/deviation |
| `Portrait` | `Body/Portrait` 96×96 | present |
| `HP_Bar` 400×28 toward centre | `Body/Column/HPBar` (P2 `fill_mode = 1`) | present; band colouring via `FighterHudModel.HpFillColor` |
| `RallyEcho_Band` | code-built `ColorRect "EchoBand"` in `BindPanel` (`FighterHUD.cs:130-141`), fed `verb.EchoPool` | promote to authored node; add reclaim flash |
| `BlockCharges_Panel` 3×20×20 | `Body/Column/PipRow/Shields`, code-built `ColorRect` pips at **16×16** (`PipSize = 16`) | size deviation |
| `StatusEffects_Panel` 2×24×24 + radials | `Body/Column/TopRow/Status` — one **Label** from `runtime.PresentedStatusType` | **Missing.** Data already present (see §0.2). Note F24 explicitly forbids reversing P2's slot order |
| `StockLayout/StockDisplay` mode-switched | `Body/Column/PipRow/Stocks` — always pips from `state.Stocks` | **Missing F21 Time-mode label.** `FighterRuntimeComponent.KnockoutsSuffered` is the authoritative stocks-lost counter (`FighterSimulationDriver.cs:1020` calls it "increments in every mode"); `MatchMode.TimeLimit` exists in `GameManager.cs:12` |
| `UltimateMeter` 48×48 radial | `Body/Column/MeterBar` ProgressBar, height 12 | shape deviation; no full-meter flash/chime |
| `DefySeal` 20×20 | — | **Missing.** `FighterVerbComponent.DefyHistoryUsed` (`FighterSimulationComponents.cs:473`) is the readable state; four states need Ready / Spent / Unavailable / (post-Defy protection) mapping |
| — | `Body/Column/Cooldowns` — four code-built 56×34 plates (S1/S2/MOV/ULT) | repo extra; keep, record deviation |

Sudden Death (F21/F22): `FighterOverlayModel` already emits a `SuddenDeathStamp` banner
(`fighter_sudden_death_stamp` = "SUDDEN DEATH"). The design now also wants the **timer area** to read
"Sudden Death — next death loses" *instead of a running clock* — that is a `FighterHUD.ApplyMatchState`
change plus one new key.

Ownership outline (F24 §Ownership): required `_OwnerOutlineColor` / `_OwnerOutlineThickness` /
`_OwnerOutlineEnabled` as an **independent persistent shader layer** (P1 `#00f0ff`, P2 `#ff3366`),
composed after status/armor glow and never recoloured by effects. Repo has one combined outline in
`assets/shaders/outline_glow.gdshader` driven by `GlowPresentationController`/`FighterProxyStyle`
(`_OutlineColor`, `_OutlineThickness`, `_GlowIntensity`, `_PulseSpeed`). **This is a shader + material
isolation task shared with the VFX/glow workstream** — flag ownership, do not silently claim it.

---

## 4. Fighter Onboarding — Move List, Systems Card, Calibration Drills

### Move List (implemented, one V7.6 delta)
`scripts/UI/MoveListScreen.cs` + `scenes/ui/MoveList.tscn` already source everything live from
`BasicComboRules.StringProfileFor`, `UniversalMovementRules`, and `{id}_data.tres` via
`AuthoredResources.Load`. Entry points exist: `PauseMenu` (`Root/Center/Panel/Layout/MoveListButton`),
`LocalFighterPause` (`MoveListP1Button`/`MoveListP2Button`), `CharacterSelect`
(`SelectPhase/Root/ButtonRow/MoveListButton`).
**Gap (H-24):** V7.6 Story lock badges — entries for abilities not yet unlocked under the Legacy Unlock
Schedule must render **"Dormant — restored after Level N"** with **no frame data**, driven by
`unlockedLegacyAbilities`. Nothing named `LegacyAbilit*` / `unlockedLegacy` exists in `scripts/`.
Change: a mode flag on `MoveListScreen.Populate` (Story vs Fighter), a lookup into the unlock schedule,
one new key family (`movelist_dormant_until`), and suppression of `movelist_ability_stats` for locked rows.
**Blocked on the Legacy Unlock workstream.**

### Systems Card (implemented, no delta)
`scripts/UI/SystemsCardScreen.cs` + `scenes/ui/SystemsCard.tscn`, eight `systems_card_*` section pairs
in `en.csv:1179-1195`. No §7 change. (Its *content* may drift with other workstreams' rule changes —
e.g. `systems_card_block_body` still says "locks it out for 5 seconds" — but that is their call.)

### Calibration Drills (F18 Option B) — the big one (H-23)
**Required:**
- New **main-menu entry** "Calibration Drills", available on first boot with **no Story save**.
- Flow: Main Menu → **single-player character picker** (all nine, full normalized Fighter kits, reusing
  roster data/portraits/controls/Move List, **without** the two-player Ready gate, 3 s countdown, or
  stage select) → **drill list** → shared Holodeck drill scene.
- Back from drill list → picker; Back from picker and "Exit Calibration" → Main Menu.
- **Retain** the Story hub console's Calibration Drills entry; same drill list/content with the campaign
  character's *normalized Fighter kit*; "Exit Calibration" returns to the originating hub.
- Neither route grants Story grid perks, Time Freeze, death rewinds, or campaign rewards.
- Drill HP/meter/cooldowns/temporary effects isolated from Story and match state; each drill supplies
  and resets only the resources its scripted lesson needs, on start and on retry.
- Hub entry preserves the active campaign attempt and its resources; drill exit applies **no** campaign
  exit fee (note: `SessionExitGuard` currently bills a 20% undeposited-dust fee on abnormal exit —
  the drill route must not trip it).
- Six drills, each pass/fail with one line of coaching:
  1. block the string then escape after Hit 2
  2. tech a launch
  3. DI a finisher
  4. grab a blocking dummy
  5. Echo Step a whiffed special
  6. reclaim a Rally echo
- Pass/fail feedback offers **Retry / Next Drill (when available) / Drill List / Exit Calibration**;
  final drill offers list or exit only.
- Quitting or relaunching never resumes a drill as a Story attempt.
- Honour remapped controls, gamepad/keyboard focus, localized coaching.

**What exists:** nothing named `Drill`. The nearest neighbours are
- `scripts/UI/HolodeckConsolePanel.cs` — a code-built in-hub *CPU practice bout* configurator
  (CPU difficulty/character/stage/mode/stocks/time/items/hazards → `GameManager.LoadScene(stage.ScenePath)`),
  reached from `HubWorldController.BuildHolodeck()`/`OpenHolodeckConsole()`. **Add a second entry to
  this panel** rather than replacing it.
- `scripts/Environment/TutorialCalibration.cs` — the Level 0 six-step calibration scripting the design
  says to reuse.
- `scripts/UI/CharacterSelectScreen.cs` (839 lines) — two-token select with Ready gate + countdown +
  stage phase; the picker must **not** reuse this wholesale, but its tile grid, portrait/stat preview,
  and `ButtonRow/{MoveListButton,SystemsCardButton}` footer are the reuse targets.
- `FighterLoadoutFactory` already accepts normalized base resources only, which is exactly the
  "no Story grid perks" requirement.

**Change list:** new `CalibrationDrillPicker` screen (scene + script), new `CalibrationDrillList`
screen, new `HolodeckDrillScene` + a `DrillDefinition`/`DrillRunner` pair driving the existing
`FighterSimulation` with a scripted dummy, a pass/fail result panel, a new session field for the return
destination (MainMenu vs originating hub), a main-menu button + screen-stack entry, a Holodeck panel
entry, ~25–35 new translation keys (drill names, lessons, coaching, pass/fail, buttons), and a new
test suite. **XL — this deserves its own agent.**

---

## 5. UI hierarchy & screen flow

### Main menu (H-25, H-26)
Required options: Story Mode, Fighter Mode, **Calibration Drills**, Settings, Quit Game.

`scenes/menus/MainMenu.tscn` `RootScreen/Center/Panel/Layout` children, in order:
`TitleFlourishTop, Title, Subtitle, TitleFlourishBottom, NoticeSlot, TopSeparator,
QuickPlayButton, StoryButton, LevelSelectButton, FighterButton, SettingsButton,
BottomSeparator, QuitButton`.

- **Missing:** `CalibrationDrillsButton` + its handler in `MainMenu.BindRootScreen()` (`MainMenu.cs:132-155`).
- **Repo extras vs design:** `QuickPlayButton` (`menu_quick_play` = "Quick Play (Einstein vs CPU)") and
  `LevelSelectButton` (`menu_level_select`, debug-only via `MainMenu.ShowDeveloperLevelSelect`).
  Both are deliberate dev affordances — record as P04 deviations, do not delete without a ruling.
- Quit confirmation: **already correct** (`ConfirmQuit` → shared `ConfirmModal`, `menu_quit_confirm`).
- LAN: already removed per V7.3; `MainMenuSceneTests.TheRootScreenCarriesNoLanButton` pins it.
  `menu_lan_match` (`en.csv:1022`) is a recorded orphan.

**M01 Fighter Play Options (H-26):** design wants a screen with **Local Versus** and **Steam Remote Play
Together** (short invite guidance, then the same local flow; entry hidden when Steam integration is
unavailable; honest unavailable message with Back/Local Versus retained if it fails after opening).
Repo: `FighterButton` routes straight to `res://scenes/menus/CharacterSelect.tscn` (clearing any lingering
`FighterOpponentType.Lan`). `scenes/menus/NetworkSelect.tscn` + `scripts/UI/NetworkSelectScreen.cs` exist
but are de-scoped to Package 7. Change: either a new authored `FighterPlayOptions` screen or a sixth
`MainMenuScreen` stack entry; plus a Steam-availability probe (no Steam integration exists in the repo —
so in practice the entry is **always hidden** today, which is the honest shipping state).

### Story flow
Save Select / New Game / Load / Delete: **all implemented** in `MainMenu` (slot rows, `save_slot_summary`,
`ConfirmModal` delete, `save_completed` banner). No §7 delta beyond copy (H-27).

### Naming (H-27)
Master now says **"Warden Time-Ship"**, **"Warden Officer"**, **"The Meridian Founding"** where the mirror
said Archive Time-Ship / Resistance Officer / Library of Alexandria Restoration. Repo copy to sweep:
`localization/en.csv` (`results_return_hub` = "Return to Time-Ship" is fine; hub/level titles, dialogue
speaker keys, `alexandria_*` keys), `scripts/Environment/HubWorldController.cs`, level 15 copy.
**Shared with the Narrative workstream** — coordinate so the rename lands once.

---

## 6. Level completion, sealing, results, credits

### N01 sealing (H-28) — the structural change
**Required:** boss defeat → defeat dialogue → regain control beside the boss's physical pickup and a
marked sealing anchor → single Interact **"Seal Timeline — Complete Level"** → one atomic transaction
(completion, tier bonus, deposit, siphon shutdown, unlocks, destination) → restoration vignette →
results. `AwaitingSeal` must persist (defeated-boss fact + ready anchor ID + at most one pending
uncollected pickup); reload before sealing keeps AwaitingSeal, reload after resumes without a second
payout; duplicate presses/callbacks/loads cannot settle twice. Anchor grants no Mending, rewind refill,
or Integrity benefit; Time Freeze/death/paused states cannot trigger or queue it. Level 0 keeps its
existing tutorial transition.

**What exists:** `StoryLevelControllerBase.OnBossDefeated` (`:939`) → `StartPostBossSequence` →
`AdvancePostBossChain` (plays `PostBossDialogueIDs`) → `StartExitSequence` (plays `ExitDialogueID`) →
`ShowCompletionResults()` (`:1024`) which sets `LevelComplete`, calls `Levels?.CompleteLevel()`,
`Audio?.ReleaseToAmbient()`, then `PresentCompletion()` → `LevelResultsPanel` with
`ReturnRequested → StoryManager.ReturnToHub()`. **Completion is automatic** — exactly what N01 forbids.

The one precedent: `Level15Controller` overrides the tail; its `TemporalCoreAnchor` (`PrimeAnchorID =
"level_15.prime_anchor"`) is an `IInteractable` + `IStoryRewindable` that is `Arm()`ed on boss death and
calls `ShowCompletionResults()` on Interact. Its docstring says armed state is **derived, never
persisted** — N01 requires the opposite (`AwaitingSeal` must survive a reload), so generalising it also
means changing that decision.

**Change list:** extract a reusable `SealingAnchor` (or generalise `TemporalCoreAnchor`), add an
`AwaitingSeal` block to the story save payload (schema bump — **Saves workstream**), insert an anchor
placement hook into `StoryLevelControllerBase` next to the existing checkpoint/extractor builders, split
`ShowCompletionResults` into `CommitSealTransaction()` + `PresentCompletion()`, add the prompt key
`seal_timeline_complete_level`, make the Interact prompt render the **live C01c binding**.

### Results overlay (H-29, H-30, H-31)
`scripts/UI/LevelResultsPanel.cs` + `scenes/ui/LevelResults.tscn`:
`Shade/Panel/Layout/{Title, DustEarned, DustMobs, DustExtractors, DustBoss, CompletionTime,
RewindsUsed, ReturnButton}` plus **three code-built lines** appended above the return button
(`IntegrityLine`, `SecretsLine`, `RatingLine`, `LevelResultsPanel.cs:171-187`).

- **H-29** F05 itemisation is now *required enemies, boss, Extractors, secret/other optional
  allocations, losses, tier bonus* — six categories against the repo's three
  (`results_dust_mobs/_extractors/_boss`). Needs new keys and a wider `ShowResults` overload; the
  itemisation values come from the economy workstream's ledger.
- **H-30 Chronal Rating is retired.** Remove `RatingLine`, `results_rating` (`en.csv:1021`),
  `StoryManager.LastLevelChronalRating` (`:270`, `:289`), `save.RatingByLevel[levelID]` (`:619`),
  and `scripts/Core/ChronalRatingRules.cs`. Leaving the save field is the safer migration; removing it
  is a schema change. **Coordinate with Saves.**
- **H-31** The Integrity line must show the value **frozen at PreBoss activation**, with the tier's dust
  bonus applied to the total. Repo freezes `LastLevelIntegrityPercent = TimelineIntegrityPercent` at
  *completion* (`StoryManager.cs:268`) — i.e. it keeps draining through the boss fight.
  **Depends on H-43 (PreBoss freeze) — Integrity/Timeline workstream.**
- **H-32 Act III:** design says steps 5–6 (Return Prompt, Hub Spawn) **do not run** for Levels 13–14; the
  next level begins in-level and the completion auto-deposit is the only Act III banking event. Repo has
  no Act III branch — `Level13Controller`/`Level14Controller` use the base tail. Shared with the Act III
  Gauntlet workstream.

### Credits / campaign completion (H-33)
`scripts/UI/CampaignCompletionSequence.cs` already does: ending dialogue → `MarkCampaignCompleted()`
**at credits start** → `CreditsController` → MainMenu; `SkipToEnd()` covers skipping. That matches
"Commit completion/rewards before credits, not when credits finish" (N01) and step 3.
**Gaps:** N05's two ending variants (clean restoration vs scarred) selected from the 15-level Integrity
average (`total >= 750` percentage points), committed atomically with the L15 sealing transaction.
Repo has `save.IntegrityByLevel[levelID]` (`StoryManager.cs:618`) — the raw data — but no average, no
threshold, no variant, and no ending presentation split. **Shared with the Narrative workstream**;
my side is the credits/flow plumbing and which variant the sequence plays.

---

## 7. Cutscenes & narrative delivery

- **H-34 Hold-to-skip.** Design: "Any sequence is hold-to-skippable — see Dialogue Mechanics, Section 16,
  for the first-viewing confirm rule." Repo (`DialogueManager.cs:301-303`) gates eligibility on
  `save.IsCompleted && save.ViewedDialogueIDs.Contains(sequence.DialogueID)` — i.e. only on a completed
  campaign *and* a previously seen sequence. V7.6 loosens this to "any sequence, subject to the §16
  first-viewing rule". The mechanics (`HoldToSkipSeconds = 0.75f`, `CanHoldToSkip`, `SkipHoldFraction`,
  `dialogue_hold_skip` hint + fill bar, `RecordLastViewedDialogue`/`ViewedDialogueIDs`) are all built —
  this is a **one-predicate change** plus whatever §16 actually specifies. **Confirm the exact §16 rule
  with the Narrative workstream before touching the predicate.**
- **H-36 Major plot cinematics** = full-screen hand-drawn stills (2–4 per beat, slow pan/zoom via
  `AnimationPlayer`) layered *under* the standard dialogue box, incl. nine per-character nexus stills over
  the shared Level 0 opening. Nothing exists — `DialogueManager` renders portraits + glass panel only, and
  there is no cinematic still surface. Design explicitly forbids `VideoStreamPlayer`. **L, content-heavy.**
- **Boss intro ritual** (name card 1.5 s + free signature telegraph + dialogue, skippable on repeat):
  partially present — `StoryHUD.ShowBossIntroCard(nameKey, seconds)` exists (code-built `BossIntroCard`
  Label, 40 px, centred) and `StoryManager` persists a boss-intro-seen set. Free telegraph / skip-on-repeat
  belong to the Enemies workstream.
- **H-37** Speaker-name copy: "Resistance Officer" → "Warden Officer" (see H-27).

---

## 8. Environmental hazards, puzzles, level template

### H-38 — F16 Option B: all Story pits/voids lethal
**Required (verbatim-heavy, this one is dense):** crossing an authored kill boundary resolves one
**non-hit** fall death (HP→0) regardless of HP, block, armor, one-hit shield, or temporary invulnerability.
Camera framing never kills. No 25%-HP penalty, no recoverable pit, no free checkpoint teleport.
**Defy History cannot prevent it** — used flag unchanged, no damage-based meter, no Rally echo.
With a death-rewind charge: spend exactly one, then the existing grounded-history rewind + difficulty HP
restore (Easy 70 / Normal 50 / Hard 30) with normal post-rewind invincibility; going 1→0 completes and
does not itself Collapse; use the **deepest valid grounded-history landing** with the existing fallback
chain (not the most recent grounded position) and validate landing support/clearance so recovery doesn't
drop the hero back inside the boundary. With no charge: Acts I–II Collapse → hub with paid checkpoint
recovery; Act III Anchor Snap if an anchor remains else Smothered. Existing Collapse dust fee only.
Integrity: live fall time drains it; crossing during Time Freeze still kills and ends the effect under its
cooldown rule; death/recovery presentation pauses Integrity but rewind never restores it; a fall-caused
Collapse/Snap uses the non-timer checkpoint allowance; locked pre-boss clock stays locked; if Integrity
hits zero on the same update, resolve **one timer-caused Collapse instead** without also spending a charge
or charging a second fee. F10 persists the unique recovery cause/result before presentation; duplicate
boundary callbacks/load cannot repeat the death, spend, or fee. Every lethal opening needs a readable
edge/depth cue, including during Time Freeze.

**What exists:** nothing. No kill boundary, no fall-death path, no `KillPlane`/`BottomlessPit` type.
`PlayerController.cs:1968` carries the "should a true blast-zone/pit death path ever be added, it must
bypass this" comment beside the Defy branch. Several levels explicitly authored **no** pits
(L12 "the Moon has no bottomless pits", L13 "there is no fall death here", L14, L15) and L04's boss pit
is "deliberately not a hazard" — so scene authoring is also required per level.
`ChronalRewindManager`'s "deepest grounded-history landing" rule already matches the required landing
selection (2026-08-15 semantics, pinned by `ChronalRewindTests`).
**L. Heavy cross-workstream coupling** (Integrity, Time Freeze, Act III anchors, F10 persistence, Saves).

### H-39 — V01b Reset Puzzle
Nearby, safely reachable **Reset Puzzle** Interact on an unsolved puzzle with losable props: restore only
registered props/mechanisms to their authored starting arrangement, preserve committed solved/latch/access
facts, no dust cost / combat-resource change / enemy or reward reset / time refund. Integrity keeps
draining during live reset feedback; **Time Freeze blocks the interaction**; validate clear geometry before
an atomic reset without displacing actors; no duplicate props or stale solve/reward callbacks; solved
puzzles cannot reset.
Repo: `scripts/Environment/PuzzleManager.cs` + `PuzzleLever`, `PressurePlate`, `WeightedObject`,
`RotatingGear`, `ConductiveCoil`, `BeamEmitter`/`BeamReceiver`, `PowerRoutingNode`, `SequenceLock`.
No prop registry, no reset path, no `IInteractable` reset anchor.

### H-40 — V01c dedicated puzzle props / intended actions
Each mechanism explicitly accepts its designated props and player actions; combat constructs, decoys,
enemies/corpses, incidental effects cannot substitute. Plates use authored puzzle weights (equal treatment
of heroes where player occupancy is intended); beam routing uses dedicated source/relay/receiver props,
**not Tesla's combat coils**; strike-operated shared controls accept the ordinary basic attack with no
combat reward; preserve narrowly authored 4A ability gates and F04 Nexus authorization; declare puzzle
ownership, accepted actions and deduplicated conditions; visually mark eligible props.
Repo: `ConductiveCoil.cs` is exactly the ambiguous case flagged (a combat coil vs a puzzle relay);
`PressurePlate` uses `WeightedObject` but needs an authored-weight/eligibility declaration.

### H-41 — Collapse Tremor (V7.6, Story only, <20% Integrity, intensifies <10%)
Telegraphed falling debris, `FractureEligible` platforms becoming Crumbling Platforms, screen tremor,
spreading chronal-crack glow. Repo has `CrumblingPlatform.cs` (shake→collapse→respawn) and
`CameraShake` autoload, but **no** `FractureEligible` flag, no tremor driver, no debris, no crack-glow
overlay. Full spec lives in §3 (Timeline Integrity) — **primary owner is the Integrity workstream**;
my side is the HUD's <20% tick/pulse and the shared crack-glow presentation.

### H-42 / H-43 — Checkpoints (F12 + V7.6)
Required: three **roles** (Entry / Middle / PreBoss) on shared levels with the existing
`{levelID}_checkpoint_{0,1,2}` IDs; **Level 4A takes exactly Entry + PreBoss, both enabled on every
difficulty**; the Hard middle-inactive rule applies **only** to shared Acts I–II — shared Act III keeps
its Hard middle active; **check enablement and boss-clock lock by authored role, never ID suffix.**
Activating any checkpoint saves `checkpointIntegrity`; **PreBoss activation additionally freezes the
gauge** and records final Integrity in that transaction (after required approach objectives); Entry/Middle
and the Eraser route trigger cannot lock the clock; an inert Hard middle saves nothing.
Repo: role is inferred from the **ID suffix** — `StoryLevelControllerBase.cs:677-680`
`SelfActivating = id.EndsWith("_checkpoint_0")`. No `CheckpointRole` enum, no Hard-middle-inert rule
anywhere (`grep` for `Inert`/`HardMiddle` finds only unrelated Mirror-Paradox code), no `checkpointIntegrity`,
no PreBoss freeze. Strike-to-activate itself **is** implemented (`CheckpointTrigger` + `StrikeSurface`
`EnvironmentHurtboxAdapter`, pinned by `CheckpointStrikeTests`/`CheckpointSaveOrderingTests`).

### H-44 / H-45 — Dust (F05)
- Extractors pay "their allocated optional-budget dust", not a flat 25.
  `scripts/Environment/ChronalExtractor.cs:19` — `[Export] DustReward = 15` with a comment that design
  targets 25 but the ledger says 15. Now it must come from the per-level F05 ledger.
- "All defeated enemies are guaranteed to drop Chronal Dust" is **retired**: each finite source pays a
  pre-authored integer allocation; zero allocations spawn nothing. Repo: `StoryDropSystem` +
  `StoryDropTable` currently guarantee a kill drop.
**Primary owner: the economy workstream.** My interest is only the results-screen itemisation (H-29) and
the HUD dust counter (H-03).

### Items & power-ups
Story drop tiers/difficulty scaling: unchanged by the diff. Orb taxonomy (five names across modes):
unchanged (already V7). **Temporal Aegis** gained D02b/D02e/D03e ordering rules (projectile immunity
first, then Aegis before HP barriers and block; one active Aegis, no expiry, duplicate consumed for
nothing; absorbed hit applies no hitstun/stagger/new statuses) — `FighterRuntimeComponent.AegisHits`
exists. **Fighter/Defensive-effects workstream, not mine** — listed here only because it sits in §7.

### Camera / grid / scrolling / room transitions / enemy spawn rules
**No diff.** `StoryCameraConfiner`, `RoomTransitionTrigger`, `FighterCamera` midpoint/zoom all conform.
No work.

---

## 9. Settings & comfort (C01)

- **H-48 Reduced Temporal Effects (C01a).** One toggle in **Settings → Gameplay**, beside Screen Shake,
  help text "Reduces distortion, after-images and screen flashes. Keeps gameplay cues and timing
  unchanged.", default **Off**. Persist `reducedTemporalEffects` in the existing global settings payload
  through versioned handling (missing legacy value → Off, without overwriting an explicit value or
  inferring from Screen Shake). Apply **before the first loading/portal effect**; apply live without
  restarting a level, replaying a proc, or resetting a timer; clear already-emitted decorative
  after-images on enable; turning Off must not replay old flashes. Covers Story, Fighter, training,
  menus/loading transitions, scripted/cinematic effects. Stored locally, outside match snapshots/hashes;
  cannot influence hit eligibility, target selection, random draws, or simulation results.
  Repo: `SettingsMenu` Gameplay tab has `HapticToggle`, `HapticSlider`, `DamageNumbersToggle`,
  `HudOpacitySlider`, `ScreenShakeSlider`, `UiScaleSlider` — **no** reduced-effects toggle, and
  `grep ReducedTemporal` finds nothing. The reduced *treatment* table (7 presentation sources) is a
  **VFX/shader workstream** obligation; mine is the setting, its persistence, and its plumbing.
- **H-49 C01c control list.** The remapping screen must list **every** gameplay action: Left, Right, Up,
  Down, Jump, Roll, Block, Basic Attack, Special 1, Special 2, Movement Ability, Ultimate, Interact, Grab,
  **Echo Step**, **Time Freeze** (labelled Story-only), Pause — one editable direct binding per device
  kind, plus On/Off controls for the fixed preset shortcuts (Ultimate = Movement+Special2 / LB+RB;
  Grab = Block held + Basic Attack; Echo Step = Block + Roll). Requires a new `gameplay_echo_step` action
  and migration of a customized `gameplay_rewind` → `gameplay_time_freeze`.
  Repo: `SettingsMenu.BuildControlsTab()` covers remappable actions with per-device-kind slots and shows
  the LB+RB chord **read-only**; there is no `gameplay_echo_step`, no `gameplay_time_freeze`, and
  `gameplay_rewind` still exists. **Primary owner: the input/settings workstream** — flagged here because
  §7's HUD/Move List/drill prompts must render live bindings (C01c: "Use device-correct, live binding
  glyphs in HUD/tooltips, move lists, tutorial/drill instructions, Nexus prompts and interaction prompts").
  `DialogueManager.DescribeInteractBinding()` is the existing precedent for that.
- **C01b audio mix priority** — audio workstream, out of scope here.

---

## 10. Shared-file / ownership matrix

**`scripts/UI/StoryHUD.cs` + `scenes/ui/StoryHUD.tscn` are contended by four workstreams.**
Recommended split — one owner does the **scene rewrite + binding skeleton** (H-01), others fill
their nodes behind a narrow API:

| HUD node | Data source | Suggested owner |
|---|---|---|
| `SafeArea`, `TopLeft_Panel`, `TopRight_Panel`, portrait, HP bar, block pips | existing | **H (this workstream)** — owns the scene rewrite |
| `RallyEcho_Band` | `PlayerController.EchoPool` | **H** (already wired) |
| `StatusEffects_Panel` + both indicators/radials | `StatusController` control/damage slots | **H** (data exists) |
| `IntegrityClock` (ClockFace / ClockWedge / SiphonStreams / IntegrityText), tier cracks, <20% tick, pause dim, PreBoss seal flourish | `StoryManager.TimelineIntegrityPercent`, live Extractor registry, PreBoss freeze | **Timeline-Integrity workstream** — H provides the named nodes and a `SetIntegrity(percent, tier, siphonCount, paused, sealed)` entry point |
| `TimeFreezeIndicator` (32×32 + remaining label; Ready / Active 5 s / Cooldown 45 s) | new Time Freeze verb | **Time-Freeze workstream** — H provides the node + a `SetTimeFreeze(state, secondsRemaining)` entry point |
| `RewindCounter` (icon, `×N`, red pulse at 1, empty at 0); **delete `RewindCooldownPip`** | `StoryManager.ChronalRewindsRemaining` | **H**, but the pip deletion must land with the Time-Freeze/F03 change so the 12 s manual rewind and its pip die together |
| `Cooldown1..3_LockOverlay` + `UltimateMeter_LockOverlay` (Dormant / Suppressed / Clear) | `unlockedLegacyAbilities`; Suppression status | **Legacy-Unlock workstream** (Dormant) + **Eraser/Null-Lance workstream** (Suppressed); H provides the overlay nodes and a `SetSlotLock(slot, state)` entry point |
| `DefySeal` (Story + Fighter) | `PlayerController._storyDefyHistoryUsed`, `FighterVerbComponent.DefyHistoryUsed` | **H**, but both flags are currently private/internal — needs a read accessor from the Combat workstream |
| `BeaconAnchors` + `AnchorPips` | Act III anchor charges | **Act III workstream**; H provides the nodes |
| `CurrencyContainer` + `DustPickup_Float` | `ChronalDustPickup.Collect()` / `OnDustAwardCollected` | **H**, values from the **economy workstream** |

**Other shared files:**
- `localization/en.csv` — every workstream. ~60–80 new keys from this workstream alone (drills dominate).
  Note the three localization gates: `ScriptTranslationKeyTests`, `SceneVisibleTextTests`,
  `UnusedTranslationKeyTests` (fails on a **new** orphan; retiring `results_rating` requires adding it to
  the recorded-orphan roster or deleting the key). **After editing `en.csv`, run
  `--headless --import` and commit the regenerated `localization/en.en.translation`** — `--quit` does not
  recompile it (CLAUDE.md).
- `AGENTS.md` — needs edits for: the two-slot status reality (the "one active status at a time" bullet is
  already stale), the retired Chronal Rating, the new HUD hierarchy, Calibration Drills, N01 sealing, the
  test baseline, and the Story kill boundary.
- `scripts/Core/StoryManager.cs` — contended by H (rating removal, results stats) and the Integrity
  workstream (PreBoss freeze, `checkpointIntegrity`).
- `scripts/Environment/StoryLevelControllerBase.cs` — contended by H (sealing anchor, completion split),
  the Integrity workstream (checkpoint roles), and the economy workstream (dust itemisation).
- `scripts/UI/CharacterSelectScreen.cs` / `scenes/menus/CharacterSelect.tscn` — the drill picker reuses its
  tile grid and footer; avoid concurrent edits with the Fighter-flow workstream.
- `scripts/UI/SettingsMenu.cs` / `scenes/ui/Settings.tscn` — H (Reduced Temporal Effects) vs the input
  workstream (C01c control list). Different tabs, but the same scene file.
- `assets/shaders/outline_glow.gdshader` + `GlowPresentationController` — H's F24 ownership-edge item vs
  the VFX workstream's reduced-effects treatment.

---

## 11. Test impact

**Will fail / need rewriting on the HUD scene rewrite:**
- `tests/unit/StoryHudPresentationTests.cs` — `TheAuthoredSceneSuppliesEveryWidgetTheControllersDriveAndAdoptsTheTheme`
  asserts the current node paths; `TheStatusPipFollowsTheRisingAndFallingEdgesOnTheBus` asserts the
  single-status pip; `BlockChargePipsAreDataDrivenAndTrackTheBus`, `CooldownAndMeterEventsDriveTheIndicatorRow`,
  `HudOpacityIsAppliedAtReadyAndAgainWheneverTheSettingChanges`, boss-notch cases should survive with path updates.
- `tests/unit/HudAbilityIndicatorModelTests.cs` — the model gains a second slot.
- `tests/unit/FighterHudSceneTests.cs` — `TheStatusPipAppearsOnlyWhileAStatusIsActive` (two slots now),
  `BarsAndPipsTrackTheComponentValuesTheyAreGiven` and `PipCapacityFollowsTheAuthoredShieldChargesAndMatchStockRule`
  (Stock vs Time display), `PlayerPanelsAnchorToTheBottomAndTheClockStaysTopCenter` (node renames),
  `TheMatchClockFollowsTheTimerFlagAndFormatsTimedModes` (Sudden Death text).
- `tests/unit/MainMenuSceneTests.cs` — `TheAuthoredSceneCarriesEveryScreenAndControlTheScriptBindsByPath`,
  `EveryInteractiveControlOnEveryScreenJoinsItsFocusChain`, `ExactlyOneScreenIsVisibleAtATime`,
  `CancelWalksTheScreenStackBackAndStopsAtTheRoot` — all move with a new Calibration Drills button and any
  Fighter Play Options screen.
- `tests/unit/LevelResultsStatsTests.cs` — rating line removal + F05 itemisation.
- `tests/unit/DialoguePresentationTests.cs` — the two hold-to-skip cases pin the completed+seen gate.
- `tests/ContentValidation/StoryHudLocalizationTests.cs`, `FighterHudContentTests.cs`,
  `SceneVisibleTextTests.cs`, `UnusedTranslationKeyTests.cs`, `UIThemeTests.cs`, `UiScaleTests.cs`.
- `tests/unit/MoveListScreenTests.cs`, `tests/ContentValidation/SystemsCardContentTests.cs` (lock badges).
- `tests/unit/HolodeckConsoleTests.cs` (new drills entry).
- `tests/unit/CampaignCompletionTests.cs`, `tests/unit/StoryLevelControllerBaseTests.cs` (sealing split).
- `tests/unit/TimelineIntegrityTests.cs`, `CheckpointStrikeTests.cs`, `CheckpointSaveOrderingTests.cs`
  (roles, `checkpointIntegrity`).

**New suites likely needed:** `StoryHudContractTests` (node-path conformance against HUD_CONTRACT),
`StatusSlotHudTests` (both modes, both slots, fixed positions, P2 not reversed),
`DustCounterPersistenceTests`, `IntegrityClockTests`, `StockDisplayModeTests`,
`CalibrationDrillFlowTests`, `SealingAnchorTests`, `StoryKillBoundaryTests`,
`ReducedTemporalEffectsSettingTests`.

**Baseline:** 1638 (verified 2026-08-29). Expect a large net add; reconcile the exact delta per
CLAUDE.md's signature-6 rule (**one `[TestSuite]` class per file**) and never accept a `Total:` without
comparing to the expected number.

---

## 12. Conflicts, blockers and open questions for the orchestrator

1. **Node-name conformance vs. churn.** The contract names (`SafeArea`, `P1_Status`, `HealthBar_BG`) differ
   from every shipped name. Either rename everywhere (breaks every scene test's paths, one time) or record
   a P04 deviation mapping repo names ↔ contract names. **Needs a ruling before H-01 starts.**
2. **Suppression is not a `StatusType`.** F24 puts Story-only Suppression in the control slot and says it
   "also retains the ability-lock overlays". The enum is `TimeDilation, Venom, StaticCharge, RadiantBurn, Root`.
   Adding a sixth member touches `StatusController`, `GlowPalette`, the sim's packed slots, snapshots, and
   `status_*` keys — **Combat workstream**, blocking H-07.
3. **Tesla's Conductive mark "stays outside both slots under F07"** (HUD_CONTRACT) — no `Conductive`
   status exists in the repo either; note it so nobody adds it to a slot.
4. **H-05/H-06 must ship together.** Deleting the manual-rewind cooldown pip without the Time Freeze
   indicator leaves the HUD with a dead 12 s mechanic; shipping the indicator without deleting the pip
   shows two temporal clocks.
5. **H-31 depends on H-43**, which depends on the Integrity workstream's PreBoss freeze. Results Integrity
   cannot be made correct in isolation.
6. **H-28 needs a save-schema slot (`AwaitingSeal`)** and contradicts `TemporalCoreAnchor`'s current
   deliberate "armed state is derived, never persisted" decision. That decision was made to avoid stranding
   the ending behind a transient flag; N01 requires persistence plus once-only commit. **Design/Saves ruling needed.**
7. **P04 authority note:** `docs/DESIGN_BUILD_DEVIATIONS.md` states no confirmed build deviations are
   recorded (the docs workspace had no game source). Everything in this dossier is first-hand evidence at
   `31fed14` and is exactly the material that ledger asks for — recommend seeding it with these findings
   rather than leaving it empty.
8. **`docs/PACKAGE5_CAMPAIGN_PLAN.md` is present in this repo** (`docs/` per CLAUDE.md's authority map)
   even though the docs workspace reports it missing and P05 deferred recovery. Worth telling the user:
   the "unavailable source" flag in the master is wrong from the repo's side.
9. **Steam is not integrated anywhere in the repo** — so M01's Remote Play Together entry would always be
   hidden. Confirm whether the entry ships hidden or the item is deferred.
10. **Two deliberate repo extras on the main menu** (Quick Play, dev Level Select) are not in the design's
    options list. Confirm they stay before anyone "fixes" the menu to match.
