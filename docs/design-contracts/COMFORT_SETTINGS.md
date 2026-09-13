# Comfort and readability settings — C01

Decision: 2026-09-12. C01a is resolved by user-selected **Option A: one Reduced Temporal Effects preset**. This is an initial-release design requirement; implementation, assets and visual/runtime validation remain pending. C01b is resolved by user-selected **Option A: one prioritized background audio treatment, without cumulative filtering/ducking, with clear warnings/dialogue**. C01c is resolved by user-selected **Option A: direct bindings for every gameplay action, with existing chords retained as optional shortcuts and no custom chord editor**. Current C01 design choices are resolved; implementation and validation remain pending.

Authority: [main design](../design-godot-v7.md), [HUD contract](HUD_CONTRACT.md), [temporal rules](TEMPORAL_STATE_CONTRACT.md), [defensive effects](DEFENSIVE_EFFECTS.md) and [rollback presentation](ROLLBACK_STATE_CONTRACT.md).

## C01a — Reduced Temporal Effects

Expose **Reduced Temporal Effects** as one toggle in Settings → Gameplay beside Screen Shake, with help text: “Reduces distortion, after-images and screen flashes. Keeps gameplay cues and timing unchanged.” Default Off retains the authored standard presentation. On selects the complete reduced treatment below; do not expose separate component sliders for this decision.

Persist the Boolean `reducedTemporalEffects` in the existing global settings payload through its versioned settings handling. A missing legacy value defaults Off without overwriting an existing explicit value or inferring it from Screen Shake. Apply it before the first loading/portal effect. It covers Story, Fighter, training, menus/loading transitions and scripted/cinematic effects. A shared local screen uses the device's global setting; future online peers can choose independently.

Apply changes live to current and newly created effects without restarting a level, replaying a proc or resetting a gameplay timer. Clear already-emitted decorative after-images and stop suppressed overlays when On is selected. Turning Off resumes only the current event's remaining authored presentation; it must not replay old flashes or generate duplicate events. Preview through ordinary UI feedback rather than automatically firing a bright demonstration.

## Reduced treatment

| Presentation source | Treatment while On |
|---|---|
| Chromatic aberration, RGB splitting, lens/refraction warping and high-frequency scanlines | Disable the distortion/scanline passes, including Relativity Rift, Death Rewind and portal/loading effects. Keep a clean, stable boundary/shape for active zones. |
| Decorative temporal ghost trails and repeated after-images | Remove trailing copies of actors/props. Preserve the single fixed Echo Step destination indicator, movement path readability and actual independently interactive entities such as decoys. |
| Full-screen KO, Ultimate, rewind and transition flashes | Replace brightness bursts, rapid polarity changes and repeated flashes with a restrained stable overlay or smooth fade plus the existing text/glyph/local impact cue. Never replace them with a differently colored full-screen flash. |
| Heavy world desaturation, pulsing vignette and flickering environmental overlays | Disable full-scene desaturation and decorative pulsing/flicker; use steady, restrained tint/edge treatment where needed to identify the time state. Preserve readable crack lines, platform warnings and static danger areas. |
| Actor armor, status, spawn and D04 Defy protection feedback | Use steady secondary glow/symbols and a smooth expiry fade instead of repeated flashing. Keep separate Fighter ownership outlines, both status slots and the spent Defy seal. |
| Attack-class signatures and telegraphs | Retain Basic/Guard-Crush/Unblockable glyphs and timing, plus the distinct Special-class projectile signature. Use a stable outline/aura or clean trail without RGB splitting; removing decorative distortion cannot make different block costs indistinguishable. |
| Loading portals and illustrated/cinematic transitions | Use the clean portal shape/static image and smooth fade in place of distorted/refraction-heavy effects. Preserve scene readiness, minimum loading-display duration, actual transitions and existing cinematic controls. |

The preset takes precedence over per-ability presentation requests such as “maximum chromatic aberration” or a full-screen flash. Apply the same rule to all nine kits, bosses, items and overlapping effects; no Ultimate or cutscene bypasses it. Art may retain readable normal colors and localized restrained impacts. The preset is a specified reduced treatment, not a claim of universal comfort or completed visual testing.

## Gameplay information and other settings

Keep damage, hitboxes, physics, animation/action durations, hitstop, gameplay slow motion, camera framing needed to show play, attack warning duration and resource clocks unchanged. Defy's presentation and post-control 60-tick protection keep their existing timing; Time Freeze stays 5 seconds with its existing 45-second cooldown and Integrity drain. Showing a calmer screen never shortens or extends these mechanics.

Preserve the fixed Echo Step target, rewind landing/result cue, freeze-active and thaw warning, boss phase cues, hazard footprints, platform fracture warnings, countdowns, status glyphs and both player-slot outlines. Do not hide danger geometry or use hue alone for attack/ownership identification. Read reduced effects from authoritative state without creating another simulation clock.

Screen Shake remains independently controlled by its existing slider; zero still disables shake. This visual preset does not overwrite shake, UI scale, HUD opacity, haptics, audio volume or audio filtering settings. Existing important sounds remain available; C01b below resolves overlapping audio treatments. Respect user volume/mute settings without forcing warnings louder.

Store and apply the preference locally, outside match snapshots/hashes. Under S01 it cannot influence hit eligibility, target selection, random draws or simulation results. Reconcile reduced visuals after rollback without regenerating suppressed effects. Graphics quality settings and scene-specific materials must respect the same preset rather than re-enabling an expensive/flashy pass.

## Pending verification

Check Off/On and switching during portals, Death Rewind, Time Freeze/thaw, Echo Step windup, Defy, all nine Ultimates, KO/Draw, Collapse Tremor, hazard warnings and simultaneous statuses/armor/projectiles. Verify both local players and an online replay with differing local presentation preferences produce identical gameplay hashes.

Review bright/dark scenes at supported small-display sizes, including Steam Deck display size, with both status slots, cooldowns, Integrity and Act III Beacon/anchor information visible. Cover 90%–140% UI scale, HUD opacity, long localized text and keyboard/controller prompts. Confirm clear attack classes, stable ownership, visible danger footprints and no overlap hiding a timer. Record visual evidence; do not declare this passed from document checks.

Verify persistence before the first portal, absent legacy value, live effect cleanup, no replay on toggling, scene changes and rollback. Confirm suppressed passes stay off even when an ability requests them, zero screen shake still works, audio/settings remain unchanged and gameplay timing/resources are identical.

## C01b — One prioritized background audio treatment

User selected Option A on 2026-09-12. Apply this mix policy by default in both visual-preset states; it is not another user toggle. Keep atmospheric time/low-health/environment effects, but select **one background treatment at a time**. Important warnings, UI and dialogue use clear routing rather than inheriting the background's muffling, pitch warp, heartbeat panning or cinematic attenuation.

### Select and release a treatment

Use a single mix controller. Gameplay/presentation states request profiles; individual scripts must not independently multiply bus gain, append filters or restore cached old volumes. Highest active priority wins:

| Priority, highest first | Background treatment |
|---|---|
| Paused game/menu | Existing pause treatment; suspend game-owned audio as required and keep menu/UI feedback clear. |
| Death Rewind / Collapse / Anchor Snap presentation | The actual active recovery's authored background treatment; Death Rewind retains its single 12 dB music duck and downward pitch. |
| Ultimate cinematic | One authored Ultimate background treatment, retaining the existing 12 dB duck for background music/world SFX. Its featured sound plays at its authored level through the clear presentation route. |
| Defy / boss phase / other scripted presentation | Use the active presentation's authored treatment. Resolve an equal-priority overlap by stable presentation/actor identity, not callback order. |
| Time Freeze | The existing subdued time-stop treatment; suspend frozen actors' action audio. Do not generate queued hazard/attack cues while the underlying simulation is frozen. |
| Low Integrity / Collapse Tremor | Its authored atmospheric treatment where one exists; this priority rule does not invent a new warning trigger or sound asset. |
| Low health | Existing below-20%-HP background treatment; heartbeat movement/muffling is confined to the background. |
| Underwater/liquid environment | Authored environmental treatment. |
| NormalGameplay | Baseline mix. |

Only actual active states request these profiles; priority does not start/end gameplay effects or alter their timers. A priority row with no authored background treatment adds none. Shared local play uses one global background selection: two low-health fighters or simultaneous Ultimates cannot double the duck or create competing pan filters. Actor-specific real cues remain separate and retain their identity.

Apply the winning profile's gain/filter/pitch targets once relative to the user's configured baseline. For example, Ultimate over LowHealth uses the Ultimate treatment alone; a prior 12 dB rewind duck cannot make it 24 dB. Blend smoothly from current parameters to the newly selected targets using authored transition envelopes. Re-evaluate current active requests when a profile ends; do not blindly restore Normal or a saved stale low-health state. Rapid transitions must not accumulate attenuation, filters or queued transitions.

Spatial reverb and musical arrangement remain authored scene/music features, but cannot introduce another low-health/time/cinematic filter or another duck. Select/blend one current environmental reverb send rather than stacking zone returns. Keep critical cues/UI/dialogue on the clear path; ordinary background world audio can retain its selected room character. Do not restart the music clock/stems merely because mix priority changes.

### Preserve useful cues and player preferences

Separate background Music and World SFX processing from a clear **Critical Cues** path and the existing UI/Dialogue path. Important attack-class/hazard warnings, impending platform danger, freeze/thaw and recovery/phase cues, timer danger cues and confirmed result announcements bypass background low-pass/pitch/panning/ducking. Preserve their event timing and useful spatial direction; do not force all warning sounds into the center. Featured cinematic sounds must not mask them.

This is not a one-sound-at-a-time rule. Keep distinct valid warnings audible together; suppress duplicate callbacks for the same event, not a different player's warning. Preserve normal gameplay pause/Time Freeze eligibility: clear routing cannot play an attack warning before the simulation actually emits it, advance a frozen loop or replay a backlog after thaw. Existing Time Freeze/rewind audio suspension and current alarm trigger conditions remain authoritative.

Clear routing still respects Master and the cue's existing user category volume/mute, including SFX for gameplay cues. Implement the split below those user gain controls, with background-only processing on the background path; moving a cue directly to Master must not bypass a muted SFX setting. Do not auto-boost UI/dialogue or counteract a user's low volume. Preserve the existing master limiter/headroom policy. If audio is muted, existing visual cues remain available; this decision adds no forced volume floor.

Dialogue's authored synthetic voice/chirp character may remain, but is not additionally warped/muffled by environmental or health profiles. An Ultimate's featured sound uses its authored mix/headroom, not a new unconditional gain boost over the user's settings.

### State, replay and verification

Derive active mix requests from authoritative current game/presentation state and apply them locally outside gameplay snapshots/hashes. On load, scene change or rollback, reconcile the current mix and looping sounds; do not replay profile-entry stingers or already presented one-shot warnings. S01's stable event IDs and confirmed result rules remain authoritative. Audio callbacks cannot trigger damage, extend a status or delay control return.

Verify low health plus underwater, low health plus Tremor, Ultimate over either, Defy during an Ultimate, recovery after a low-health state and pause/unpause over each profile. Exercise both local fighters, profile expiration in different orders, rapid transitions, scene/load/rollback and repeated identical events. Measure that repeated transitions return to the selected target without cumulative attenuation/filtering.

Listen on supported speakers/headphones and representative small-device output, at normal and reduced user volume. Check critical warnings, both fighters' cues, dialogue and cinematic sounds against dense combat; verify category mute, Master mute, clear-route spatial direction and no background leakage into protected cues. Confirm Time Freeze produces no frozen-action backlog and visual C01a On/Off changes no audio policy. Record mix settings and evidence; routing/mix implementation and listening/runtime sign-off remain pending.

## C01c — Direct bindings and optional preset shortcuts

User selected Option A on 2026-09-12. The remapping screen lists every gameplay action: Left, Right, Up, Down, Jump, Roll, Block, Basic Attack, Special 1, Special 2, Movement Ability, Ultimate, Interact, Grab, Echo Step, Time Freeze and Pause. Offer one editable direct-binding override per device kind, with keyboard/mouse separate from gamepad. Keep existing analog/directional defaults and default aliases where no override is saved; this decision adds no analog calibration or multi-binding editor.

Ultimate, Grab and Echo Step can each receive a single key/button. Time Freeze has its ordinary direct binding and is labelled Story only; it can be configured globally but never becomes usable in Fighter. Display locked/not-yet-unlocked actions for configuration without granting their ability. Preserve normal input costs, cooldowns, action-state/mode restrictions, F04 Nexus authorization, F19 CPU policy and Time Freeze action locks.

### Defaults and shortcuts

| Action | Direct defaults | Optional preset shortcut |
|---|---|---|
| Ultimate | Keyboard U; gamepad direct slot initially Unbound | Gamepad Movement Ability + Special 2 (LB+RB at defaults), enabled by default. No new keyboard Ultimate chord is enabled by default. |
| Grab | Direct slots initially Unbound; existing explicit user bind retained | Block held + Basic Attack press, enabled for keyboard/mouse and gamepad. |
| Echo Step | Direct slots initially Unbound | Block + Roll under the existing timing/recovery rule, enabled for keyboard/mouse and gamepad. |
| Time Freeze | R / Back or Select, preserving migrated user overrides | No additional shortcut; Story-only action. |

Expose the existing preset shortcuts with an On/Off control per applicable device kind. Their **action recipes** are fixed; render their current bound buttons and let component action remaps update them. There is no arbitrary chord-capture/editor UI. A recipe uses that device's currently effective component bindings, including retained defaults/aliases, without combining inputs from different player devices. Labels such as LB+RB describe defaults rather than hard-coded keys that ignore remaps.

A direct bind and enabled shortcut can coexist. Disabling a shortcut leaves its component actions operating normally; pressing its components then cannot invoke the disabled combined verb. Do not disable someone's shortcut automatically when assigning a direct bind. Show the enabled access routes clearly.

### Resolve the verb once

Direct input and a recognized shortcut produce the same semantic action request, deduplicated per actor/tick. Do not implement a direct Grab/Echo Step/Ultimate by synthesizing presses of its component buttons: this could accidentally block, roll, move or cast Special 2. A refused direct action does nothing and spends nothing; it cannot turn into a component action.

Both routes enter the existing shared legality/priority resolver. Retain accepted chord precedence over component singles, legal Echo Step over block-cancel in attack recovery, and the authored illegal-grab-chord fallback to a legal block-cancel. Disabling a chord removes that chord candidate, not the ordinary component inputs. Giving a verb a direct binding grants no stronger priority, extra cancel, new timing leniency or bypass of resources. Preserve the existing Ultimate chord arbitration and action ordering.

For example, a direct Echo Step still requires own attack recovery, 30 meter, a ready cooldown and a valid historical target. Direct Grab retains its normal grounded/capture exclusions; it cannot escape a held grab. Direct Ultimate retains 100-meter/F04 eligibility. Pressing direct and shortcut routes together can execute/spend only once.

Capture remaps in the settings UI with gameplay input suspended there; the captured press cannot also activate the rebound action. Apply changes atomically at a safe input boundary, clear stale buffered/held-edge state for affected bindings and require fresh presses rather than synthesizing an action from a still-held key. Do not change a running online match's controls through an unavailable/unnegotiated pause flow. S01 serializes resulting logical action requests/recognition state, not local hardware key codes; version protocol input capabilities when adding direct actions. Carry direct-versus-preset request origin and the component candidates needed for the existing illegal-chord fallback. Peers replay that normalized input, never re-recognize a remote chord using their own shortcut settings. Add the direct Echo Step action (`gameplay_echo_step`) and its explicit logical intent; do not reuse the reserved Dash bit or claim that no input/schema change is needed. Final numeric IDs/bit allocation require inspection of actual formats.

### Conflicts, prompts and persistence

Reject duplicate direct bindings between simultaneously available gameplay actions on the same device and identify the conflict by action name. A declared preset's intentional sharing with its component actions is not an ordinary duplicate-binding error. Check shortcuts against their own distinct component bindings and active mode; never accept an ambiguous duplicate merely because one action is currently locked in the campaign.

A device configuration must retain a reachable input route for required supported actions: either a direct bind or a valid enabled preset. Do not silently leave Ultimate/Grab/Echo Step unreachable when their shortcut is disabled. Explain the missing route and keep the last valid configuration until the user assigns a direct bind or re-enables the shortcut. Time Freeze is required for the Story control profile but remains unavailable in Fighter. Preserve menu navigation/cancel access while remapping Pause or clearing an action; reject a layout that traps the user in the editor. No bindings are silently stolen to resolve a conflict.

Use device-correct, live binding glyphs in HUD/tooltips, move lists, tutorial/drill instructions, Nexus prompts and interaction prompts. Prefer the direct binding in a single-action prompt when present; otherwise show the enabled preset's full current chord. A move list can show both. Describe Echo Step and Grab by action name, not mandatory hard-coded Block+Roll/Block+Attack instructions. Remapping changes how the verb is requested, never the text explaining its eligibility.

Persist direct overrides, explicit Unbound choices and shortcut enable flags in versioned global settings, not campaign/gameplay snapshots. Keep device-specific changes independent. Do not keep claiming schema v4 already supports the added representation: inspect actual saved formats and assign a versioned migration during implementation. Legacy missing flags reproduce the existing shortcuts; retain valid direct binds and migrate customized `gameplay_rewind` to `gameplay_time_freeze` under F03 without overriding an explicit newer Time Freeze bind. Preserve source data and show repair needs rather than silently discarding conflicts.

An explicit Unbound value differs from a missing override that inherits defaults. Validate the entire proposed profile before committing it. Per-action reset restores that action's direct default and preset flags for the selected device; global reset restores the whole control profile. Preserve unrelated audio/visual settings, and update all glyphs after load/migration/reset.

### Pending verification

Check keyboard/mouse, gamepad and both local player/device assignments; all direct actions, enabled/disabled presets, remapped components, default aliases, duplicate direct+chord requests, conflicts, unavailable actions and unreachable-layout rejection. Verify no synthetic component action on a refused direct request, exact existing costs/cooldowns/cancels and the authored chord fallback cases.

Test migration from missing flags and existing Grab/Ultimate/custom Rewind binds, explicit Unbound versus inherited default, per-device/per-action/global reset, interrupted save and active held inputs during rebind. Inspect move lists, drills, F04 and UI prompts for stale hard-coded buttons. Confirm S01 logical input replay reproduces all direct/shortcut outcomes without importing device bindings, including peers with different local shortcut preferences and illegal-chord versus refused-direct fallback. Implementation, schema migration and input/runtime validation remain pending.

## Validation status

Current status: C01a–C01c specified; implementation, per-effect art/mix treatment, settings/input migration and visual/listening/input/runtime validation pending.
