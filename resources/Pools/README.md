# Pool configuration resources

Scene pool warm-up is data driven through `ScenePoolConfig` resources and `scene_pool_catalog.tres`.

Current prototype budgets:

- `tutorial_pool_config.tres`
- `florence_pool_config.tres`
- `test_arena_pool_config.tres`

`GameManager` resolves the pending scene through the catalog and asks the persistent `PoolManager` to warm the validated configuration before scene replacement. Every definition has a stable pool ID, template, warm-up count, hard maximum, and overflow policy.
