# Pool configuration resources

Scene pool warm-up is data driven through `ScenePoolConfig` resources and `scene_pool_catalog.tres`.

Current prototype budgets:

- `tutorial_pool_config.tres`
- `florence_pool_config.tres`
- `test_arena_pool_config.tres`

Package 4 added per-level warm-up budgets for the campaign levels that Package 5 will author:

- `level_pool_configs/level_02_pool_config.tres` through `level_15_pool_config.tres`

These are **not** in `scene_pool_catalog.tres` yet. The catalog maps a scene path to a config, and
`scenes/campaign/Level_02..15` do not exist; Package 5 adds each catalog row alongside its scene.
Until then the configs are validated on their own by `tests/ContentValidation/ScenePoolConfigTests.cs`
(budget validity plus a worst-case-encounter assertion: at most 30 warm standard+elite enemies and
40 warm enemy projectiles per level). Warm-up counts are concurrency budgets, not level totals —
`standard_enemy`/`elite_enemy` are shared scene pools and the `EnemyData` is assigned per spawn, so
adding roster variety to an era does not change its pool sizing. Per-level numbers and rationale are
in `docs/PACKAGE4_ROSTER_PLAN.md` §8 C1.

`GameManager` resolves the pending scene through the catalog and asks the persistent `PoolManager` to warm the validated configuration before scene replacement. Every definition has a stable pool ID, template, warm-up count, hard maximum, and overflow policy.
