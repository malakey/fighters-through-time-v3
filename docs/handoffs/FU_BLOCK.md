# FU-BLOCK handoff - a clear block-success effect (2026-10-25)

## 1. Summary
A successful block now plays a short drawn effect at the blocker's front in both modes: a cyan-white shield arc with a facet flash and sparks for an ordinary block, a gold-white arc plus a double ring, a brief core flash and more sparks for a perfect block, and red-orange shards with a broken ring for a guard break. Presentation only; no sim, rules, save, protocol, HUD, menu or localization change. Nobody has looked at it in play.

## 2. What shipped
- `scripts/Combat/BlockSparkEffect.cs` (+ `scenes/vfx/BlockSparkEffect.tscn`): `enum BlockSparkKind { Ordinary, Perfect, Shatter }`; `BlockSparkEffect : PooledNode, IPoolable`, drawn in `_Draw()` (`DrawArc`, `DrawLine`, `DrawColoredPolygon`, `DrawCircle`), `QueueRedraw()` each `_Process` while alive, no textures or particles. Static `Show(kind, worldPosition, facingRight, parent, scale = 1f)` registers its own pool (warm-up 4, capacity 16, RecycleOldest) like `FloatingDamageNumber.Show`; `OnSpawn`/`OnDespawn` reset every field. Read surface: `Kind`, `IsActive`, `Duration`, `Age`, `Facing`.
- Durations: ordinary 0.28 s, perfect 0.40 s, shatter 0.45 s. Colours: ordinary (0.65, 0.95, 1.0); perfect (1.0, 0.92, 0.35) = the `UIPalette.GoldBright` family, with white; shatter (1.0, 0.38, 0.12) and (1.0, 0.65, 0.25).
- Ordinary: ~110 degree arc centred on the facing direction, radius 22 -> 38 px, line 6 -> 1 px, fading; an inner thin white arc; a hexagonal translucent facet (radius 16) for the first 0.08 s; 6 spark streaks. Perfect: the same arc, a full double ring expanding to 70 px and 52 px, a white core circle (radius 14) for the first 0.06 s, 10 sparks. Shatter: a dashed ring (8 segments) growing 12 -> 46 px and 7 triangular shards under gravity (420 px/s^2) with spin; no arc.
- `ComfortSettings.FullScreenFlashAllowed` false: no core flash and the facet fill alpha is halved (0.55 -> 0.27). Nothing strobes; no text, so no localization.
- Story: `scripts/Characters/PlayerController.BlockFeedback.cs` (`ShowBlockSpark(BlockResult)`; kind from `GuardBroken` / `_blockSystem.LastBlockWasPerfect`; position = `GlobalPosition + (facing * 28, -42)`, constants `BlockSparkForwardPixels` / `BlockSparkRisePixels`; parent = the hero's own parent). One-line hooks: `PlayerController.cs` block branch (beside the shake) and `PlayerController.EnvironmentalBlock.cs` (`TryBlockEnvironmentalHit`). Covers the Mirror clone.
- Fighter: `scripts/FighterSim/FighterSimulationDriver.BlockFeedback.cs` (`ShowBlockSpark(player, kind)`; `+ (facing * 30, -46)`, `BlockSparkScale` 1). Three one-line hooks in `FighterSimulationDriver.cs`: the charge-drop branch (ordinary), the daze edge (shatter), and `SyncPerfectBlockFlash` (perfect; it gained a `PlayerController player` parameter, private, one caller).

## 3. Tests: 3529 + 11 = 3540
- `tests/Unit/BlockSparkEffectTests.cs` (3): per-kind duration then return to the pool; a reused effect starts clean; `Show` with no parent spawns nothing.
- `tests/Unit/StoryBlockSparkTests.cs` (5): ordinary (kind, position, facing), perfect, guard break (`BlockChargeCost` 99) shows exactly one shatter, hazard block, unblocked hit shows none.
- `tests/Determinism/FighterBlockSparkTests.cs` (3): a real blocked-swing run shows ordinary sparks and exactly one shatter at the daze edge, ordinary before shatter; a seeded rise of the 323 perfect-block count shows one perfect spark; an ordinary hit shows none.
- No existing test changed. Observed: full run `Failed: 0, Passed: 3540, Total: 3540`, 1 m 52 s; `[TestCase]` count under `tests/` is 3540.

## 4. Provisional numbers
All sizes, durations, spark counts, colours and offsets are agent-chosen from the plan and tunable (`PROVISIONAL-BLOCK-SPARK-LOOK`); the Fighter scale is 1.0 and was not judged against the Fighter camera framing.

## 5. Shared files touched / merge notes
`PlayerController.cs` (+1 line in the block branch), `PlayerController.EnvironmentalBlock.cs` (+1 line), `FighterSimulationDriver.cs` (3 hook lines, one signature change to `SyncPerfectBlockFlash`). FU-KNOCK's new driver partial should merge cleanly; my hooks are inside the three block branches (lines ~843-860, ~900-925). New `.uid` sidecars are committed.

## 6. Not delivered / risks
Not seen in play: legibility at the 1.5 Story zoom and the Fighter camera, and whether it reads when the blocker's front is near a wall. The Fighter driver has no contact point, so the effect is at a fixed offset, not where the hit landed. Rollback double-fire is theoretical (LAN de-scoped; edge caches as for the existing cues). No core audio cue (FU-ECHO owns the catalog). Enemies and bosses have no block stance and show nothing.

## 7. Drafted doc changes
- AGENTS.md, Combat, the block bullet ("Blocking stops movement ..." / the V7.3 block-model sentence), append: "**Since 2026-10-25 a successful block plays a code-drawn `BlockSparkEffect` at the blocker's front in both modes** (`USER-BLOCK-SUCCESS-EFFECT`, `PROVISIONAL-BLOCK-SPARK-LOOK`): an ordinary block a cyan-white shield arc, a facet flash and sparks (0.28 s), a perfect block a gold-white double ring with the arc and more sparks (0.40 s), a guard break red-orange shards and a broken ring (0.45 s); pooled, presentation only, gated on `ComfortSettings.FullScreenFlashAllowed` for its bright fills (`PlayerController.BlockFeedback.cs`, `FighterSimulationDriver.BlockFeedback.cs`; suites `BlockSparkEffectTests`, `StoryBlockSparkTests`, `FighterBlockSparkTests`)."
- GDD (Block / feedback lines): "A successful block is announced by a short on-body effect: a shield arc and sparks for an ordinary block, a gold double ring for a perfect block, shards for a guard break; shape as well as colour distinguishes them."
- Ledger rows. `USER-BLOCK-SUCCESS-EFFECT`: intended - "When a block is successful there is a visual animation that clearly indicates it" (user request 2026-10-25); observed at this branch's head - shipped as above; owner Unassigned; acceptance - a hands-on pass confirms each of the three outcomes reads at once in Story and Fighter. `PROVISIONAL-BLOCK-SPARK-LOOK`: intended - agent-chosen shapes, colours, sizes, durations and offsets; observed - the numbers in section 2; owner Unassigned; acceptance - the user approves or retunes the look after a hands-on pass, including the Fighter scale.
