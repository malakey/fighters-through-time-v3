# Recon E — block-success feedback and hook points (2026-10-25)

Read-only subagent report at `0eba848`, from code only.

## 1. Story today
- A block resolves in `PlayerController.ResolveIncomingHit`; the block branch is `scripts/Characters/PlayerController.cs:1677-1713` and calls `_blockSystem.ResolveHit(hit)` at `:1678`. `ResolveHit` is `scripts/Combat/BlockSystem.cs:120-170`.
- **Ordinary block** (`BlockSystem.cs:153-166`): spends charges, resets the regen timer, sets `_shieldStunTimer` from `BasicComboRules.ShieldstunFramesFor(damage)`, calls `GrantHenrysBastion` and `EventBus.RaiseBlockAbsorbed(PlayerIndex, charges)`. `PlayerController.cs:1693-1708` adds `CameraShake.Shake(3f, 0.08f)`, `HapticFeedbackManager.OnGuardImpact` (a 0.06 s buzz, `HapticFeedbackManager.cs:26-28,143`), a 2-frame hitstop and Punish-window bookkeeping. No sprite flash, spark, sound or number; the only visual is the `block` pose (`PlayerController.cs:3755`). `OnBlockAbsorbed` has only gameplay listeners (`FirstUseTooltipPresenter.cs:79`, `Level00Controller.cs:128`).
- **Perfect block** (`BlockSystem.cs:144-151`): `_owner.NotePerfectBlock()` (`PlayerController.PerfectBlock.cs:114-117`) increments `PerfectBlockCount` and calls `_glow.FlashHit(PerfectBlockFlashColor (0.85, 1, 1), 0.12 s)` (`BlockSystem.cs:116-118`); `RaiseBlockAbsorbed` also fires; the shake and haptic still run. `GlowPresentationController.FlashHit` (`:289-295`) is a brief full-sprite tint override — the only perfect-block visual, so the two cases are barely distinguishable.
- **Shatter** (`BlockSystem.cs:168`, `BreakGuard` ending at `:347`): the owner goes `Dazed`, the push velocity is assigned, `EventBus.RaiseBlockBroken` fires (listeners: `HapticFeedbackManager.cs:76` heavy buzz, the tooltip and Level 0). No shatter visual beyond the dazed pose; the freeze is `ShatterFreezeFrames`.
- **Hazard blocks:** `PlayerController.EnvironmentalBlock.cs:109` calls `ResolveHit` on a Hazard-class payload — a separate call site.
- **Enemies and bosses** have no block stance, only damage reduction (`EnemyController.TakeDamage`, `:1962-1988`, `ResolveFrontalReduction`, the `Executor.ShieldDamageReduction` bubble) — out of scope.
- **Mirror clone:** a `PlayerController`, so the same path (not verified to call `ResolveHit` specifically).

## 2. Fighter today (`scripts/FighterSim/FighterSimulationDriver.cs`)
- `_PhysicsProcess` → `Simulation.Advance(...)` → `SyncPresentation()` (`:306-308`, `:417-423`); per-player feedback is `SyncPresentationFeedback` (`:784`).
- **Ordinary block** (`:837-847`): the driver compares `state.BlockCharges` with `_presentedBlockCharges[playerID]`; a drop with `DazeFrames <= 0` triggers `CameraShake.Shake(3f, 0.08f)` and `OnGuardImpact`. No visual.
- **Perfect block** (`:897-922`, `SyncPerfectBlockFlash`): reads 323 via `Simulation.TryGetFighterActionState`, takes `FighterBlockTimingRules.PerfectBlockCount` (the wrapping count in `BlockTiming` bits 20–23), fires on a rise against `_presentedPerfectBlockCount` (−1 first-read sentinel): `glow.FlashHit(PerfectBlockFlashColor, 0.12)` plus the shake and haptic. A perfect block spends no charge, so the charge-drop branch does not fire for it.
- **Shatter** (`:849-859`): a fresh daze edge plays a haptic only.
- **Contact point:** the sim exposes no "blocked this tick" event and no contact position. Blocking is front-facing only (`BlockRules.IsHitInFront`), so place the effect at the blocker's `player.GlobalPosition` plus ~30 px in its facing direction, y ≈ −46 (the HP damage number uses the same convention, `:831-834`).
- A blocked hit deals 0 HP, so the damage-number path (`:816-835`) never fires; `FloatingDamageNumber.Show` renders integers only.
- The driver reads components and writes only Godot nodes and autoloads.

## 3. Reusable infrastructure
- `GlowPresentationController.FlashHit(color, secs)` (`:289`): a full-sprite tint, no outline flash.
- `VfxEmitter` (`scripts/Combat/VfxEmitter.cs`): `Emit(poolID, pos, parent, tint)` resolves `combat_vfx` / `fighter_vfx`, falling back to registering `PooledVfxTemplate.tscn` (`:179-200`); `EmitScene(PackedScene, ...)` (`:60-72`) self-registers a pool keyed by scene path (warm-up 1, capacity 32), so a new effect needs no pool-config row. `FloatingDamageNumber.Show` (`scripts/UI/FloatingDamageNumber.cs:63-78`) also self-registers its pool — a second precedent.
- `scenes/vfx/`: `VfxBurst`, `VfxImpact`, `VfxSlash`, `VfxBeam`, `VfxShockwave`, `VfxSummon` (`VfxImpact` is the nearest fit for a spark).
- `ParticleBudget` matters only for particles. `CoreMechanicCueCatalog` has no block cue.
- `ComfortSettings`: `FullScreenFlashAllowed`, `ScreenTintPulseAllowed`, `GhostTrailsAllowed` all equal `!ReducedTemporalEffects` (`ComfortSettings.cs:88-112`); a small shape-based spark that fades quickly avoids the photosensitivity concern.
- `TelegraphGlyph` (`scripts/Combat/TelegraphGlyph.cs`): a `Node2D` drawing with `_Draw()` primitives (`DrawArc`, `DrawLine`, `DrawPolyline`) and `QueueRedraw()`, keeping shape as a colour-independent channel — the direct precedent.

## 4. Tests to model on
`tests/Unit/StoryPerfectBlockTests.cs`, `tests/Determinism/FighterPerfectBlockTests.cs`, `tests/Unit/StoryBlockModelTests.cs`, `StoryBlockClassificationTests.cs`; presentation: `tests/Determinism/FighterPresentationSyncTests.cs` (`:235` asserts only the block-charge mirror), `GlowPresentationControllerTests`, `VfxEmitterTests`, `PoolManagerTests`. The Fighter flash has no direct test. Child-count assertions exist in 18 test files (mostly UI or stage); a pooled effect parented under the arena or level avoids leaking grouped nodes.

## 5. Recommendation
- A small pooled `Node2D` (`BlockSparkEffect`) drawing with `_Draw()` (the `TelegraphGlyph` pattern), hand-tweened radius and alpha; no textures, no particle budget. Vary shape and colour by outcome: ordinary → a small arc; perfect → a bigger double ring; shatter → radial shards. Gate brightness on `FullScreenFlashAllowed`.
- Pooling: follow `FloatingDamageNumber` (a static `Show(...)` that registers its pool then spawns) or `VfxEmitter.EmitScene`.
- **Story hook:** at `PlayerController.cs:1693`, beside the shake and haptic, with the blocker's position plus a forward offset (or `hit.HitOrigin`), `_blockSystem.LastBlockWasPerfect` and `blockResult`; the shatter can hook the same site or `BlockSystem.BreakGuard` / `RaiseBlockBroken`. The hazard path (`PlayerController.EnvironmentalBlock.cs:109`) needs its own call.
- **Fighter hook:** the charge-drop branch (`:843-847`), `SyncPerfectBlockFlash` (`:919-921`), the daze-edge branch (`:853`) for the shatter; position and facing from the player proxy.
- **Rollback safety (inferred):** existing cues edge-detect against cached values (`_presentedBlockCharges`, `_presentedPerfectBlockCount`, `_presentedDazeFrames`), so a correction that lowers and re-raises a value would refire; LAN is de-scoped, so this is theoretical. The effect never touches sim state.
- A text pop would need a new Label path and keys; shape alone is simpler.
