# UR-HUDF handoff (pulp UI redesign, Phase B: the Fighter HUD, HUD-F2 "top versus bars")

Branch `ur/hudf` from `main` at `22219329`. Presentation only: nothing under `scripts/FighterSim/` changed, no snapshot, hash or protocol change, no `KLOTHO_DET002`.

## 1. What shipped

- **`scenes/ui/FighterHUD.tscn`** (regenerated; the tree below `SafeArea` keeps every pinned path). Both clusters hang from the top corners (`P1_Status` offset 36 / 22, `P2_Status` mirrored), the panels have no plate of their own, a `TopScrim` gradient (UI_SPEC 4.5 layer 2, 300 px) sits behind everything, a `TimerPlate` (ink `PanelContainer`, 240 x 118) sits behind `MatchClock`, `ModeLabel` under it, and `P1_Slots` / `P2_Slots` hang at y 192 (margin 46). The old glyph Labels (`SlotBadge`, `Damage/ControlStatus_Indicator` and their radials, `DefySeal`, `S1/S2/Movement_Cooldown` with their `Radial`, `StocksLabel`, `ShieldsLabel`) stay at their paths as **hidden carriers** (`visible = false`): tests, tooltips and accessibility read their `Text` / `TooltipText`, and the icons show the shape. `StatusEffects_Panel` and `Cooldowns` are hidden too; a hidden parent leaves each Label's own `Visible` true, which `StatusSlotReserved` reads.
- **`scripts/UI/FighterHUD.cs`** builds the Pulp widgets (`BuildWidgets`): `PulpPortraitRoundel` 110 (the `Body/Portrait` TextureRect keeps the texture and is hidden), the 30 px slot shape (`slot_p1` / `slot_p2` icon at 22) beside the name, the "P1" tag (`CaptionLabel`, upper-case), the HP bar (`PulpBar` variation, scene-local `bar_track_h44` background through `PulpKit.Plate`, fill left the variation's `StyleBoxFlat` so `TintFill` works, `PulpBarDecor` for tracker / echo / hatch / ticks / low glow), the HP numerals ("86" `HudNumeral` + "/110" `HudNumeralSmall`, inner side), the 390 x 16 meter bar (`bar_track_h16`, fill `Meter2`, `PulpBarDecor`, plus a `FullGradient` `GradientTexture2D` #F3C95B to #FFF0B0 that shows only at a full meter, no text), stocks as `star4` 28 in the slot colour (off 0.22), block pips as `shield` 24 in `Shield` (off 0.28), the Defy icon 32 (`defy_ready` res2 / `defy_notready` and `defy_spent` ink3 / `defy_barred` danger), the three 54 px `PulpAbilitySlot`s with S1 / S2 / MOV key tags and `AbilityIconLibrary` art (cooling sweep and seconds from the authoritative cooldown), and the two 40 px `PulpStatusChip`s (damage left, control right for both players; time text in tenths). P2 mirrors the order, S1 on the outer edge.
- The **Rally echo** is the decor's `EchoValue` (= HP + `FighterHudModel.EchoBandFraction` x max); the code-built `EchoBand` ColorRect is gone, `EchoBandWidth(p)` now reads the decor.
- **Hit flash and tracker:** a HP drop calls `PulpBarDecor.DropLagTo` and `PulpMotion.HitFlash(bar, per-panel HitFlashLimiter, now)`: once a second per bar, none under Reduced Temporal Effects.
- **Timer plate:** `MatchClock` is `TimerDigits`; Sudden Death swaps it to `StateLabel` with word wrap and the `Danger` colour (F22 text unchanged); the 10 s danger tint and cue unchanged (colour now through `RemoveThemeColorOverride`, so the role's ink shows otherwise). The plate and mode label follow the clock's visibility.
- **Fit (`ApplyLayout`):** at a canvas narrower than the clusters need, each HP bar gives up half the shortfall (`FighterHudModel.HpBarWidth`, floor 240; the meter keeps the 390 : 502 ratio) and the chips drop their text column below a 1700 canvas (`ChipsShowText`). At 1.4 the HP bars are about 375 px.
- **`FighterHudModel.cs`:** `HpFillColor` full band `Cyan` -> `UIPalette.Hp`; new layout constants and rules, `StatusIconName`, `DefyIconName` / `DefyIconTint`, `ModeLabelKey`.
- **`FighterOverlayModel.cs`:** the countdown numeral colour `Cyan` -> `UIPalette.P1` (inventory line 111). `FighterPresentationOverlay` itself is unchanged: its banners are centred in the screen, so the KO stamp, countdown and Sudden Death banner do not meet the top band (capture `hudf_ko`).
- **`localization/en.csv`:** block `# UR HUDF`: `fighter_hud_mode_stock` ("Stock match"), `fighter_hud_mode_time` ("Time match"). `en.en.translation` not committed.
- **Captures:** `scripts/Diagnostics/UiCapture/UiCaptureTargets.Hudf.cs`: `hudf_versus`, `hudf_time_mode`, `hudf_sudden_death`, `hudf_ko`, `hudf_140` -> `D:\Projects\ftt-scratch\ur-captures\hudf\`.

Stage-hazard warnings are world-space proxies and audio cues; no HUD text lives in the top band besides what is listed. `FighterCamera` is untouched: the top 250 px of the screen is HUD, and the camera's midpoint framing keeps fighters mid-screen (not verified on a live stage).

## 2. Tests: delta +5 from 3602

New suites: `FighterHudLayoutRulesTests` 2 (pure rules: the fit rule at 1920 and 1920 / 1.4, floor, meter ratio, chip text; distinct kit icons for every status and Defy state, mode key), `FighterHudTopBandTests` 3 (widgets at spec sizes and following components, the hit flash once a second, bounds at UI scale 1.0 and 1.4). 3602 + 5 = **3607**. Full run: `Failed: 0, Passed: 3607, Total: 3607`, 2 m 19 s, lock held. Build 0 errors, no new warnings.

Changed in place:
- `FighterHudSceneTests.PlayerPanelsAnchorToTheBottomAndTheClockStaysTopCenter` -> `PlayerPanelsAnchorToTheTopAndTheClockStaysTopCenter` (top anchors, offsets 22 / 36 / -36; the P2 `fill_mode`, P1 fill mode and clock checks kept; the P2 meter fill mode is not asserted).
- `FighterHudModelTests` lines 74-75: `Cyan` -> `UIPalette.Hp` (full HP band).
- `FighterMatchPresentationTests` line 34: countdown colour `Cyan` -> `UIPalette.P1`.

No other test changed; every pinned path in `FighterHudContentTests` still resolves.

## 3. Provisional values and choices

- HP / meter bar left edge sits at the column edge (the spec puts the 502 px track 8 px in); the 8 px is not reproduced, only the plate's own expand margin.
- Slot row separation is 20 for slots, chips and the 10 px gap (spec: chips 18 apart, slots at pitch 74 = 20).
- Chip text drops below a 1700 canvas (provisional; both players with two statuses would otherwise overlap at 1.4). Status time text uses `common_seconds_short` ("2.0s").
- Low-HP glow threshold reuses `HpDangerFraction` 0.25 (the spec's threshold is data); bands Warning and BossRed stay as shipped.
- The hit flash modulates the whole `ProgressBar` (`SelfModulate`), so the fill goes black for the frame too, not only the plate.
- The full-meter gradient is an unskewed rect over the inner area (the fill is skewed).
- Name colour is the `HeroName` role (ink); ownership rides the shape icon and tint.

## 4. Kit gaps (for the closeout)

- `PulpBar` ships the 30 px track; HP and meter override `background` in code with `PulpKit.Plate("bar_track_h44" / "bar_track_h16")`. Fold into two theme variations (`PulpBarH44`, `PulpBarH16`) with a gradient-capable fill.
- The `PulpBarDecor` cannot draw a gradient fill; the full-meter gradient is a child TextureRect. A decor `FullGradient` option would remove it.
- `PulpBarDecor` and the numerals overlap when the bar is narrow (P2 at 1.4: echo band under "63/90").
- No Reduced-Temporal-Effects capture, no HP "thunk" on the numerals (`PulpMotion.Thunk` not wired), no ready "bloom" on slots (the shipped ready flash tween is kept).
- The ability-slot art is the roster's ability icon; the HUD hides the art while Dormant/Suppressed only if a state sets it (Fighter never does).

## 5. Capture comparison (vs `mix_fight.png`)

Matching: top-corner clusters, 110 roundels with halftone, name in Cinzel with the "P1" tag and slot shape, the 44 px skewed HP bar with hatch, ticks, highlight, tracker and echo, outer-edge fill (P1 left, P2 right), numerals on the inner side, meter bar under the HP bar (gold gradient when full), stocks / shields / Defy row, the ink timer plate with "STOCK MATCH", the slot row with 54 px slots, key tags, cooling numerals, status chips with rings and upper-case names, S1 on the outer edge for P2. Differences: the mock's meter sits at 390 px but ends flush with the HP bar's track start (8 px offset not reproduced); the mock's key tags are absent from its Fighter slots (ruled to keep them); stage backdrop is the placeholder plate, not live art; the mock's slot art is the real VFX crop. `hudf_140` shows both HP bars shrunk clear of the plate, the chips without text, and the slot rows inside the top 350 px. `hudf_ko` shows the KO stamp centred, clear of the band.

## 6. Not seen

Live matches (no driver run: the HUD was driven by hand-built components), a real rollback or hit flash in motion, the low-HP glow pulse, Reduced Temporal Effects, UI scale 0.9, a stock loss, an Open stage, the HUD over a real stage with `FighterCamera` framing, gamepad play, the post-match results over the new HUD.

## 7. Drafted amendments

**`HUD_CONTRACT.md`.** *Placement:* "The Fighter HUD hangs from the top of the screen: one cluster per player at the top corners (36 px side margin, 22 px top), the match timer plate at the top centre, and a slot row per player under its cluster. The bottom of the screen is left free for Open-stage pits." *Meter:* "The Fighter Ultimate meter is a 390 x 16 bar directly under each HP bar, filling from the outer edge; a full meter shows a gold gradient and no number. (The Story Ultimate stays a ring.)" *Sizes:* "Fighter HP bar 510 x 44 (the track 502 wide), portrait 110, status chips 40 with a text column, cooldown slots 54, stock stars 28, block pips 24, Defy icon 32, slot shape 30 (glyph 22). Where the width is short (UI scale above about 115 %) the HP and meter bars shrink before they meet the timer and the chips show their badge and ring only." *Cooldowns:* "Each player's three cooldowns (S1, S2, Movement) are 54 px square slots in a row under the cluster, S1 on the outer edge, each with a small S1 / S2 / MOV key tag, a clockwise pie sweep over the elapsed share and the remaining seconds in the centre; the sweep replaces the 20 px radial and is the readiness radial for the contract's purposes. A refused press flashes the slot (D10)." *Status slots:* "Fighter status chips are 40 px badges with a ring and a name / time column, damage left and control right for both players."

**`design-godot.md`.** `:3384` "Placement: Bottom of screen, evenly spaced" -> "Placement: top of screen, one cluster per top corner, the timer plate at the top centre, slot rows under the clusters". `:3387` HP "400 x 28" -> "510 x 44". `:3534` Fighter Ultimate "48 x 48 circular indicator" -> "390 x 16 bar under the HP bar, gold gradient at full".

## 8. Process note

One capture command ran after its lock-acquire loop timed out (10 minutes without the lock) and then executed `rmdir` on the lock directory it did not own; another agent's run may have lost its lock for that moment. The later test runs acquired the lock properly (the loop now only proceeds on a successful `mkdir`).

## 9. Review fixes (orchestrator round)

- **Row order.** Row 2 (stocks, block pips, Defy) now sits between the HP bar and the 390 x 16 meter bar, both players, P2 mirrored (UI_SPEC 3L.e / 3L.f): in the scene the `MeterBar` is the last child of `Column`, after `PipRow`. No test pinned the sibling order.
- **Clock.** `FighterHUD.ApplyMatchState` passes the minutes zero-padded (`ToString("D2")`) to `fighter_hud_clock`; the key is unchanged. Sudden Death shows no time, so nothing to pad. Tests changed in place (pinned text): `FighterHudSceneTests.TheMatchClockFollowsTheTimerFlagAndFormatsTimedModes` "8:00" -> "08:00", "1:05" -> "01:05"; `SuddenDeathReplacesTheRunningClockWithItsPhaseText` "0:12" -> "00:12".
- **Echo vs numerals at 1.4.** The numerals keep their ink outline (the `HudNumeral` role) and a bar narrower than `FighterHudModel.HpMaxTextMinWidth` (460) drops the "/max" label, so only the current value sits beside the echo band; `hudf_140` shows "86" and "63" clear of it. The echo band still touches the digits at the bar's edge; it no longer sits under them.
- Re-captured `hudf_versus` and `hudf_140` and checked both. Full run `Failed: 0, Passed: 3607, Total: 3607` (2 m 13 s), delta unchanged (+5).
