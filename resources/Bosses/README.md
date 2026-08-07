# Boss resources

Canonical `BossData` resources belong here; their attack kits are `EnemyAbilityData` resources under `Abilities/{boss_id}/`. Do not reuse `FTT.Combat.AbilityData` — that resource requires a `CharacterID` and feeds Fighter loadouts, which boss content must never touch.
