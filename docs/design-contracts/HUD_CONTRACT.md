# HUD and fighter identification — F24, Option A

Decision: 2026-09-12. This is the shared design contract for HUD summaries, dimensions, hierarchy and ownership/effect rendering. Main-document and HTML summaries must be derived from these rules; validate mirrors against them when changing the contract. No automatic documentation generator is claimed. Implementation, artwork and visual/runtime checks remain pending.

## Status slots

| Mode | Geometry | Display |
|---|---|---|
| Story | Two 28 × 28 px slots below block charges | Damage slot left; control slot right |
| Fighter | Two 24 × 24 px slots beside each player's HP bar | Damage slot left; control slot right, including P2 rather than reversing their semantic order |

Hide an empty slot's icon/radial while retaining its reserved position. Use a fixed-position StatusEffects_Panel so hiding one TextureRect does not shift the other. Show both occupied slots simultaneously; never choose a single winning HUD icon based on outline priority.

Bind each slot's icon and radial duration to the authoritative status type, duration and intensity in that category. Same-slot replacement/refresh follows the existing status system; rendering does not stack, extend or recreate gameplay effects. The damage slot covers Venom and Radiant Burn; the control slot covers Time Dilation, Static Charge, Root and Story-only Suppression. Radiant Burn remains vulnerability, not periodic damage. Suppression also retains the ability-lock overlays. Tesla's Conductive mark stays outside both slots under F07.

Use distinct status glyphs plus localized names in the existing reference UI; color alone must not identify an effect. Update from actual state on apply, expiry, overwrite, recovery, loading and rollback. Radials follow authoritative simulation clocks, including pauses and Time Freeze; the HUD must not independently count down a frozen status.

## Chronal Dust

During active Story-level HUD display, the top-right Chronal Dust counter remains visible, including at zero and during the boss fight after the Integrity clock hides. Bind it to the current level's undeposited levelChronalDust balance. Previously deposited spendable dust remains separately identified in the Repository/Beacon; never display a combined balance that suggests current earnings can be spent there.

Only the pooled +N pickup notification rises and fades after one second. The numeric balance does not fade after inactivity. Update it from committed wallet changes on pickup, fee, restart, recovery, load and completion; a mirrored event or rollback must not add dust again. Pickup notifications are cosmetic and cannot drive the balance. Full-screen menus/cinematics may use their existing HUD visibility rules; persistent means no pickup-triggered or inactivity fade while that HUD is displayed.

## Ownership and effects

For visible Fighter combatants, the thin ownership outline is a separate persistent layer: P1 cyan #00f0ff, P2 red #ff3366 at the existing one-pixel reference thickness and static intensity. Future P3/P4 colors remain deferred. Match slot identity drives this layer; damage, statuses, armor, invulnerability, ability decoys and an effect expiring cannot recolor, pulse, disable or replace it. Keep the existing P1/P2 HUD labels.

Status and armor feedback uses a separate interior glow/flash and existing sparks, particles and HUD glyphs. Compose the ownership edge after these effects and keep bloom/flash intensity low enough that the edge remains distinguishable. Do not paint a second opaque effect-colored outline over it. This does not require the alternative permanent overhead P1/P2 badge.

Within the secondary effect layer only, retain the existing armor/spawn-protection precedence over status glow. Resolve simultaneous armor and spawn protection in a stable order (spawn protection, then armor); with both status categories occupied, use control glow before damage glow, while both HUD icons remain visible. On expiry, recompute from current state rather than restoring a stale cached color. PointLight2D uses the secondary effect color/intensity and never changes the ownership edge.

The existing _OutlineColor, _OutlineThickness, _GlowIntensity and _PulseSpeed controls describe the secondary effect treatment; for Fighters, its mask cannot cover the ownership edge. Add independent _OwnerOutlineColor, _OwnerOutlineThickness and _OwnerOutlineEnabled bindings for that edge. These are design bindings requiring shader/resource implementation. Story entities can retain their existing effect outlines; Story Suppression still smothers the gold resonance aura. No Fighter Suppression source is added.

Keep each actor's visual state isolated. Initialize ownership on spawn/slot assignment and restore it from match identity on rollback; do not mutate a shared material resource so that changing one actor recolors another. Choose a supported per-instance or preallocated isolated-material implementation and verify batching/memory cost; parameter changes alone do not establish a no-allocation or batching guarantee. Sample glow lifetime from the real protection/status state, not a duplicate hard-coded timer. F22's Sudden Death starts without spawn protection. D04's brief post-Defy hit protection uses the same secondary invulnerability-glow layer and its actual remaining phase/timer. Keep the F13 Defy seal broken during and after the window; glow expiry does not restore availability. Do not add a damage/control-slot icon, new persistent counter or ownership-edge recoloring. A load cannot replay the proc or restart its glow.

## Reduced Temporal Effects — C01a

The [comfort preset](COMFORT_SETTINGS.md) replaces decorative distortion, repeated after-images, full-screen flashes and heavy desaturation/pulsing with stable cues. Preserve the P1/P2 ownership edge, both status glyphs/radials, attack-class signatures, hazard footprints, clocks and the single fixed Echo Step target. Armor/spawn/Defy feedback becomes steady secondary glow with a smooth expiry fade; the Defy seal stays broken. No preset toggle changes authoritative timers or replays an effect. Screen Shake, HUD opacity and UI scale remain independent; validate small displays and long localized labels under both presentations.

## Acceptance checks

Check Story with zero/one/two statuses, either slot expiring or being overwritten, Suppression, Time Freeze, boss entry and zero dust. Verify dust remains visible while only +N fades, and check fees/load/restart against the economy ledger.

Check both Fighter slots and mirrored characters with both statuses, armor, spawn protection, flashes and persistent objects active. P1/P2 ownership must remain distinguishable when effects start, overlap and expire, including rollback and Sudden Death. Test both damage/control icons simultaneously and their fixed positions.

Review bright/dark backgrounds, supported color/contrast options, reduced-effects settings where available, HUD opacity, 90%–140% UI scale, long localized labels and small displays. Validate readability, per-actor material isolation and rendering budgets in the actual game. These checks remain pending; documentation consistency is not visual or runtime sign-off.
