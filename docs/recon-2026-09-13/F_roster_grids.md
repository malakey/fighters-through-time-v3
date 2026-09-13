# Recon dossier F — §5 Character Roster, Combat Kits & Talent Trees (V7.6 grid redesign)

**Scope:** master lines 2319–2899 (`D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`), plus the
Resonance Grid rules subsection at master ~428–522, plus `docs/DESIGN_BUILD_DEVIATIONS.md`.
**Repo state read:** `D:/Projects/Fighters Through Time - V3` @ `31fed14` (V7.4 pass, 2026-08-29), clean tree.
**Verdict:** all nine `.tres` grids must be re-authored from scratch; `ResonanceNodeData` needs three new
fields; `StoryStatProfile`/`ResonanceProgression` need an ability-scope dimension plus three new stat lanes;
the grid UI needs a real topology renderer (it is a flat 3-column `GridContainer` today); nine traversal
flags and eight reworked Majors need new code; ~162 `en.csv` rows are replaced.

---

## 1. Executive gap summary

| Area | Designed (V7.6) | Current build | Gap size |
|---|---|---|---|
| Grid topology | 9 unique per-character layouts (mesh, spine, gear rings, circuit, three acts, scale, delta, fence, crossing currents) | Nine identical "3 branches × 3 nodes" chains | **All 9 `.tres` rewritten** |
| `prerequisiteMode` | `All` / `Any` enum on every node | Field does not exist; `EvaluateUnlock` hardcodes All-of | Schema + resolver |
| Ability-scoped stat keys | `StatModifier.abilityScope` string; `AbilityDamage/AbilityRange/AbilityDuration/ConstructHP` | Flat `StatModifierKey` string; scope faked by roster-generic lanes (`ZoneRadius`, `PersistentDuration`, …) | Schema + resolver + ~8 call sites |
| `gatedAbilityID` (dormant silhouettes) | Node hidden/dim until the Legacy ability unlocks | **No Legacy Unlock Schedule exists anywhere in the repo** (`grep LegacyUnlock` → 0 hits) | Blocked on §3 agent |
| Traversal nodes | 9 rule-flag nodes, one per character, Tier 2 | Zero exist; no per-character movement identity rule is implemented at all | 9 new behaviours |
| Retired stat keys | `Armor`, `BlockDurability` ("Block Health"), `BlockRecovery`, `ComboSpeed`, `JumpForce` (jump-height), stun-duration minors | All still authored: 11 nodes across 7 characters | Data + test |
| New stat keys | `RallyEchoFraction` (×4 grids), `UltimateBuildRate` (×1), `ExtractorDamage` (declared, unused by minors) | None exist | Resolver + 3 call sites |
| Major perk effects | 8 of 27 changed, 2 fully rewritten | 27/27 have code hooks and constants (good news) | See §8 |
| Cost / node count | 9 nodes, 50/75/200, 975 total | Identical — **unchanged** | none |
| Cheapest-first-Major | 325–475 dust, varies per character | Uniform 325 | data-only |
| Grid UI | Constellation with solid/dotted edges, tier positions, dormant silhouettes | 3-column `GridContainer` of 330×150 buttons; navigation is row-major `(index, ±1 col, ±1 row)` arithmetic | **Largest single UI item** |

**Good news to bank:** every one of the 27 Major perk keys already exists as a `const string …PerkKey` with a
live `Owner.HasStoryPerk(...)` consumer, and `ResonanceProgression.CollectUnlockedAbilityModifiers` already
collects **any** node with a non-empty `AbilityModifierKey` — not just Majors. So the nine traversal flags need
**no new plumbing**: author them as Minor nodes carrying an `AbilityModifierKey`, and they land in
`PlayerController.StoryAbilityPerks` through the existing `CharacterFactory` path.

---

## 2. Files that own this section

| Path | Role |
|---|---|
| `D:/Projects/Fighters Through Time - V3/scripts/Environment/ResonanceNodeData.cs` | 21 lines. Node schema. |
| `.../scripts/Environment/ResonanceGridData.cs` | 103 lines. Grid schema + `ResonanceUnlockResult` + `StoryStatProfile` (22 lanes). |
| `.../scripts/Environment/ResonanceProgression.cs` | 299 lines. Load / evaluate / unlock / respec / `Resolve` / perk collection. **The single resolver.** |
| `.../scripts/UI/ResonanceGridPanel.cs` | 359 lines. Code-builds node buttons into the authored scene's `GridContainer`. |
| `.../scripts/UI/ResonanceGridNavigation.cs` | 28 lines. Pure row-major clamp helper. |
| `.../scenes/ui/ResonanceGrid.tscn` | Script-less authored scene; binds `Center/Panel/Layout/{Balance,Status,NodeGrid,CloseButton}`. |
| `.../resources/Resonance/{9}_grid.tres` | 105–106 lines each; 9 `SubResource` nodes + the grid resource. |
| `.../scripts/Characters/CharacterFactory.cs` L61–87 | The **only** place `StoryStatProfile` → `PlayerController` fields are copied (22 assignments). |
| `.../scripts/Characters/PlayerController.cs` | 22 `Story*Multiplier` fields; `StoryAbilityPerks` HashSet; Zealous Vigor + Shield of Orléans inline. |
| `.../scripts/Characters/Abilities/*.cs` | 23 of the 27 perk hooks. |
| `.../scripts/Combat/BlockSystem.cs` | Henry's Bastion (L117–133), Quantum Entanglement (L181–207), `DepleteCharges`. |
| `.../scripts/Combat/BasicComboRules.cs` | Cross-mode rulebook. Holds Cleopatra's finisher-Venom intensity and Tesla's finisher Static Charge in `StringProfiles` — **shared with the Fighter sim, so Story-only scaling must happen at the application site, not here.** |
| `.../resources/Content/content_manifest.csv` | 9 `ResonanceGrid` rows + 1 `UIScreen,resonance_grid` row. |
| `.../localization/en.csv` | 185 `resonance_*` rows (162 node + 23 UI). |

---

## 3. Schema changes needed

### 3.1 `ResonanceNodeData`

Current (all 10 fields):
```csharp
SchemaVersion(=1), NodeID, DisplayName, DisplayNameKey, Description, DescriptionKey,
Type(Minor|Major), UnlockCost(=100), PrerequisiteNodeIDs, StatModifierKey,
StatModifierValue, StatModifierIsPercent, AbilityModifierKey
```

Required additions:

| New field | Type | Why |
|---|---|---|
| `PrerequisiteMode` | `enum PrerequisiteMode { All, Any }`, default `All` | Meshes/deltas/crossings/Three Acts. Used by Einstein (×6), Leonardo (×1), Shakespeare (×3), Pocahontas (×1). Default `All` keeps every other node's authoring unchanged. |
| `AbilityScope` | `string` (default `""`) | `relativity_rift`, `tesla_coil`, `yoricks_lament`, `venom`, `finisher_venom`, `conductive_finisher_mark`, … `""` = character-wide. |
| `Tier` | `int` (1 / 2 / 3) **or** keep inferring | **Must be explicit.** The current test infers Tier 1 ⇔ zero prerequisites. V7.6 breaks that in four grids: Joan `A1` (T1, 50 dust, prereq `S2`), Leonardo `M1` (T1, 50, Any-of), Tesla `W1` (T1, 50, All-of two T1s), Lincoln `A1`/`B1` (T1, 50, prereq `R0`), plus Lincoln `C` (T2, 75, prereq a T1). Cost alone (50/75/200) is a workable proxy but an explicit field is safer. |
| `GatedAbilityID` | `string` | Dormant-silhouette rule. **Blocked:** no ability-unlock schedule exists in the repo. Author the field now; render it as a no-op until §3's Legacy Unlock lands. |
| `IsTraversal` | `bool` (or derive from `Type` + `AbilityModifierKey` + Tier 2) | Design calls traversal a distinct node type. Cheapest: add `ResonanceNodeType.Traversal` to the existing enum — but note `ResonanceNodeType` is serialized as an int in the `.tres` (`Type = 1` == Major), so **append `Traversal = 2`**, never reorder. |

Bump `SchemaVersion` to 2 on both node and grid resources.

### 3.2 `StatType` enum

The design specifies a C# enum; the repo uses **free-form strings** (`StatModifierKey = "MoveSpeed"`) matched
in a `switch` inside `Resolve`. Recommendation: **keep strings** (the `.tres` authoring and the
`GetUnlockedStatTotal(grid, save, "PersistentDuration")` API both depend on them) and instead add a
compile-time `public static class ResonanceStatKeys` of `const string`s plus a validation test. Converting to a
real enum is a larger, gratuitous churn across `AbilityZoneTalentTests`, `ResonanceProgressionTests`, and
four ability scripts.

### 3.3 Key mapping — designed → existing resolver lane

| Designed key (V7.6) | Existing lane in `StoryStatProfile` | Action |
|---|---|---|
| `MaxHP` | `MaxHPBonus` | keep |
| `MoveSpeed` | `MoveSpeedMultiplier` | keep |
| `BasicAttackDamage` | `BasicDamageMultiplier` | keep |
| `AttackRange` | `AttackRangeMultiplier` | keep |
| `CooldownReduction` (now ability-scoped) | `CooldownMultiplier` (character-wide) | **needs scope**; Joan piercing, Lincoln splitting, Mozart wave |
| `AbilityDamage` + scope | `SpecialDamageMultiplier` / `ProjectileDamageMultiplier` / `StatusIntensityMultiplier` | **new scoped lane**, 6 uses |
| `AbilityRange` + scope | `ZoneRadiusMultiplier` / `PersistentRangeMultiplier` | **new scoped lane**, 3 uses |
| `AbilityDuration` + scope | `ZoneDurationMultiplier` / `PersistentDurationMultiplier` / `StatusDurationMultiplier` / `GlideDurationMultiplier` | **new scoped lane**, 5 uses |
| `ConstructHP` + scope | `PersistentHealthMultiplier` | rename/scope, 1 use (Leonardo turret) |
| `RallyEchoFraction` | — | **NEW lane.** 4 uses (Einstein, Joan, Shakespeare, Lincoln) |
| `UltimateBuildRate` | — | **NEW lane.** 1 use (Mozart `N3`) |
| `ExtractorDamage` | — | **NEW lane, declared but unused by any minor.** The three extractor effects are Majors (Leonardo/Tesla/Lincoln). Author the lane for future-proofing; do not invent a minor. |
| `BlockCharges` | `BlockChargeBonus` | keep (lane survives; no V7.6 node uses it) |

**Simplest implementation that satisfies the design:** keep the 22 existing lanes for the character-wide
keys, and add **one dictionary** `IReadOnlyDictionary<(string key, string scope), float>` to
`StoryStatProfile` for the scoped keys, with an accessor `GetScoped(key, scope, default: 1f)`. That avoids
adding 6×N fields to `PlayerController` and `CharacterFactory` (currently 22 hand-copied assignments — adding
scope per-field would explode it). Call sites then read
`player.StoryScoped("AbilityRange", "relativity_rift")`.

---

## 4. What the resolver (`ResonanceProgression`) must change

1. **`EvaluateUnlock` prerequisite loop** (L58–60) is All-of only:
   ```csharp
   foreach (string prerequisite in node.PrerequisiteNodeIDs ?? Array.Empty<string>())
       if (!unlocked.Contains(prerequisite)) return ResonanceUnlockResult.MissingPrerequisite;
   ```
   Needs the `Any` branch (`if none satisfied → MissingPrerequisite`; empty list still = root-eligible).
   Design mandates **one shared evaluator** for UI availability, purchase, respec dependency checks and save
   validation — today `EvaluateUnlock` is already that single point (UI calls it in `ActivateNode`/`Refresh`),
   so this is a contained edit. ~15 lines.
2. **Ability gate**: add a `GatedAbility` check to `EvaluateUnlock` returning a new
   `ResonanceUnlockResult.AbilityLocked`. Blocked on §3; stub as always-unlocked with a TODO.
3. **Authored-data validation**: design requires "reject missing prerequisite IDs and cycles". Today only a
   test checks prerequisite existence; nothing checks cycles. Add a validator (test-side is sufficient).
4. **`Resolve`** (L133–167) switch gains the three new keys and the scoped bucket. The dead-key comment block
   (`BlockDurability`, `Armor`) can be deleted once the data no longer carries them.
5. **`PercentValue`** (L295–296) is a no-op identity function — `node.StatModifierIsPercent ? v : v`. Harmless
   today because every percent node stores a fraction, but it means `StatModifierIsPercent` is decorative.
   Worth noting; do not "fix" it without auditing all authored values.
6. **Respec (`RespecAll`)** needs no change for V7.6 itself, **but** F08 mandates a one-time versioned free
   respec migration for pre-F08 Shakespeare saves whose purchased Major is missing one of its new Act II
   prerequisites. Since every node ID changes anyway (see §9), a simpler and stricter migration is: **on load,
   drop any `GridProgress` node ID not present in the character's current grid and refund its recorded cost.**
   That covers all nine characters, not just Shakespeare. Needs `StorySaveData` schema attention
   (currently `GridProgress: Dictionary<string, List<string>>` — no cost record, so refund must be looked up
   from a retired-ID→cost table or simply refunded at the old tier price).

---

## 5. What the grid UI must change

**Today's layout authoring: there is none.** `ResonanceGridPanel.LoadActiveGrid` (L192–206) iterates
`_grid.Nodes` in array order and `AddChild`s a `Button(330×150)` into a `GridContainer`; `GridColumns = 3`
is a `private const int` in the panel. Navigation is `ResonanceGridNavigation.Move(index, dCol, dRow, 3, count)`
— pure index arithmetic, no adjacency awareness. There is no edge rendering at all: prerequisites are
conveyed only by tooltip text (`resonance_state_locked_prerequisite`).

To satisfy V7.6 the panel needs:

| Change | Notes | Size |
|---|---|---|
| **Per-node authored position** | Add `[Export] Vector2 LayoutPosition` (normalized 0–1) to `ResonanceNodeData`, or a parallel `Vector2[] NodeLayout` on `ResonanceGridData`. Without it, nine "unique topologies" cannot render. **This is the single most important schema addition the design's own snippet omits.** | M |
| Replace `GridContainer` with a `Control` + absolute placement | `ResonanceGrid.tscn` node path `Center/Panel/Layout/NodeGrid` is bound by name in `ResonanceGridPanel` **and asserted by `ResonanceGridSceneTests.TheAuthoredSceneCarriesEveryControlThePanelBindsByPath`** — changing the type breaks that pin. | M |
| Edge drawing | `Line2D`/`_Draw()` per prerequisite edge; **solid for All-of, dotted for Any-of**; glow when satisfied. | M |
| Adjacency navigation | Replace row-major `Move` with nearest-neighbour-in-direction over `LayoutPosition`. `ResonanceGridNavigation` (28 lines, 3 tests) is fully rewritten or retired. | S |
| Dormant silhouettes | Gated node renders dim, unlabeled, **in its true position**; gains name/cost/tooltip on ability unlock. Blocked on §3. | S (once gate exists) |
| Tooltip requirement text | Design: "explicit requirement text in the tooltip". `PrerequisiteName` (L334–344) returns the *first unmet* prerequisite only — wrong for Any-of ("requires any of A, B") and for Shakespeare's All-of-three. | S |
| Mozart's musical-staff presentation | Design says his scale "is drawn as a musical staff in the UI". Treat as Package 10 art polish, not a blocker. | defer |

`ResonanceGridPanel` also hardcodes `CustomMinimumSize = new Vector2(330, 150)` per button — fine for a 3×3
grid, impossible for a 9-node mesh. Node chips need to shrink (~200×90) or become icon+tooltip.

---

## 6. Per-character node tables — designed vs current

Legend: **T** = tier; **mode** = prerequisiteMode; cost is 50 (T1) / 75 (T2) / 200 (Major) throughout.
"Current" rows are the shipped `.tres` at `resources/Resonance/{id}_grid.tres`.
⚠ = retired stat key. ★ = new behaviour with no code today.

---

### 6.1 Einstein — *The Spacetime Web* (mesh; every node has two routes in)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current node (same slot) | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Momentum | 1 | — | `MoveSpeed` +2% | — | `einstein_u1` MoveSpeed 0.02 | **identical** |
| 2 | Minor Chalk Edge | 1 | — | `BasicAttackDamage` +5% | — | `einstein_o1` **SpecialDamage** 0.05 | key change |
| 3 | Minor Mass | 1 | — | `MaxHP` +15 | — | `einstein_d1` MaxHP 15 | **identical** |
| 4 | **Extended Float** ★ | 2 | Momentum **or** Mass (**Any**) | flag: Warp reduced-gravity float window **+20 frames** | Movement | `einstein_u2` ZoneRadius 0.1 | **new node** |
| 5 | Minor Rift Range | 2 | Chalk Edge **or** Momentum (**Any**) | `AbilityRange` +10% scope `relativity_rift` | Special 2 | `einstein_u2` ZoneRadius 0.1 | scope only |
| 6 | Minor Rally Resonance | 2 | Mass **or** Chalk Edge (**Any**) | `RallyEchoFraction` +10% | — | `einstein_o2` ProjectileSpeed 0.1 | **new lane** |
| 7 | **Event Horizon** | M | Rift Range **or** Rally Res. (**Any**) | +20% E=mc² vs rift-trapped **+ projectiles crossing the rift slowed to half speed** ★ | Special 2 | `einstein_u3` `event_horizon` | effect +½ |
| 8 | **Critical Mass** | M | Rift Range **or** Extended Float (**Any**) | E=mc² burst applies Radiant Burn **vulnerability 1.25× for 3 s, no periodic damage**; applied *after* resolving the triggering hit; not on projectile contact / blocked / invulnerable; damage slot, stronger-wins, coexists with rift Time Dilation | Special 1 | `einstein_o3` `critical_mass` | ordering + gating |
| 9 | **Quantum Entanglement** | M | Extended Float **or** Rally Res. (**Any**) | on **shatter**, auto-warp backward **before the 1 s daze lands**; 5 s lockout unchanged | — | `einstein_d3` `quantum_entanglement` | near-compliant |

⚠ Retired here: `einstein_d2` `BlockDurability` 0.1 ("Minor Block Health +10%"), `einstein_o2` `ProjectileSpeed`.
Cheapest first Major: **325**.

---

### 6.2 Joan — *The Charge* (single forward spine, **no root choice**)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Zeal | 1 | — (**the only root node**) | `BasicAttackDamage` +5% | — | `joan_m1` BasicAttackDamage 0.05 | identical value |
| 2 | Minor Vigor | 1 | Zeal (All) | `MaxHP` +20 | — | `joan_d2` MaxHP 20 | prereq changes |
| 3 | Minor Martyr's Resolve | 1 | Vigor (All) | `RallyEchoFraction` +10% | — | — | **new lane** |
| 4 | **Wings Refresh** ★ | 2 | Vigor (All) | flag, **75 dust**: a **direct** Hit 3 or Righteous Smite hit resets Ascendant Wings cooldown to 0. Once per attack *execution* even on multi-target. No trigger on whiff / blocked / invulnerable / DoT tick / prop. Does **not** restore air jumps, add glide time, cancel the action, or bypass Suppression/Time Freeze. Respec removes future eligibility only. | Movement | — | **new node + new behaviour** |
| 5 | Minor Smite Damage | 2 | Wings Refresh (All) | `AbilityDamage` +10% scope `joan_righteous_smite` | Special 1 | — | new |
| 6 | Minor Piercing Cooldown | 2 | Smite Damage (All) | `CooldownReduction` −10% scope `joan_divine_piercing` | Special 2 | — | new (scoped) |
| 7 | **Unstoppable Crusade** | M | Smite Damage (All) | hyper-armor on Smite active frames + 1.5 s knockback/hitstun immunity after cast | Special 1 | `joan_r3` | **unchanged** |
| 8 | **Zealous Vigor** | M | Martyr's Resolve (All) | **REWORKED:** finisher reclaims an **extra 25% of the current Rally echo pool** on top of the damage-scaled reclaim. V6 "heal 5% missing HP" **retired**. | — | `joan_m3` — heals `missingHP × ZealousVigorMissingHPFraction` at `PlayerController.cs:720` | **rewrite** |
| 9 | **Shield of Orléans** | M | Piercing Cooldown (All) | **REWORKED (F06 Option A):** **+5 meter per distinct successfully blocked hostile attack** (cap 100). Requires ≥1 charge consumed; a shattering block still qualifies. Nothing on hold-block / whiff / behind / unblockable / invuln / zero-charge refusal. "Distinct" = one attack execution → track source attack ID; each basic-string strike counts once, a multi-hit ability/volley/zone/piercing projectile grants **at most one**. Flat perk event — **not** damage-taken meter and **not** a 25% multiplier. **Plus: first successful block of a Guard-Crush execution refunds one block charge** (cap 3), after normal consumption and any shatter; refund does not cancel daze/shieldstun/5 s lockout, and the refunded charge is unusable until the lockout ends. | — | `joan_d3` — `_ultimateMeter.AddFlat(hit.Damage × PointsPerDamageTaken × 1.25)` at `PlayerController.cs:567` | **rewrite + new attack-ID dedup** |

⚠ Retired here: `joan_r1` MoveSpeed ("Minor Move Speed +5%" — V6 called it *Dash Speed*, already dead),
`joan_r2` **JumpForce** (jump-height minor), `joan_m2` **ComboSpeed**, `joan_d1` **Armor**.
Cheapest first Major: **350** (Zealous Vigor). Unstoppable Crusade 450; Shield of Orléans **525** (roster max).

---

### 6.3 Leonardo — *Interlocking Gears* (two rings meshing at a third tooth; one true capstone)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Brush Reach | 1 | — | `AttackRange` +8% | — | — | new |
| 2 | Minor Compass Slam | 1 | — | `BasicAttackDamage` +8% | — | — | new |
| 3 | Minor Workshop Vigor | 1 | Brush Reach **or** Compass Slam (**Any**) | `MaxHP` +15 | — | — | new; **T1 with prereqs** |
| 4 | Minor Spiral Range | 2 | Brush Reach (All) | `AbilityRange` +10% scope `leonardo_golden_ratio` | Special 1 | `leonardo_a2` ZoneRadius 0.1 | scope only |
| 5 | Minor Turret Plating | 2 | Compass Slam (All) | `ConstructHP` **+25%** scope `leonardo_clockwork_turret` | Special 2 | `leonardo_e1` PersistentHealth **0.15** | key + value |
| 6 | **Re-placement** ★ | 2 | Workshop Vigor (All) | flag: a deployed turret can be **picked up and re-placed once** | Special 2 | — | **new node + new behaviour** |
| 7 | **Master Stroke** | M | Spiral Range (All) | Golden Ratio +15% and each tick pulls toward centre | Special 1 | `leonardo_a3` | **unchanged** |
| 8 | **Clockwork Overdrive** | M | Turret Plating (All) | 5 bolts instead of 4, **and bolts deal double damage to Chronal Extractors** ★ | Special 2 | `leonardo_e3` — 5 bolts done; no extractor rule | +extractor |
| 9 | **Daedalus Wings (capstone)** | M | **All-of** Re-placement + Spiral Range + Turret Plating | steam trail + glide→down-air cancel | Movement | `leonardo_m3` | effect unchanged; **prereq All-of-3** |

⚠ Retired: `leonardo_a1` SpecialDamage, `leonardo_e2` ProjectileDamage, `leonardo_m1` GlideSpeed,
`leonardo_m2` **JumpForce**.
Cheapest first Major: **325** (Master Stroke). Daedalus **575** (requires all six minors).
Note the en.csv description for `leonardo_e3` says "five rapid bolts instead of **three**" — design says 4→5.
Pre-existing copy bug; fix in the rewrite.

---

### 6.4 Tesla — *The Closed Circuit* (two rails + a bridge that needs both live)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Conductivity | 1 | — | `MoveSpeed` +3% | — | `tesla_w1` MoveSpeed 0.03 | identical |
| 2 | Minor Cane Edge | 1 | — | `BasicAttackDamage` +5% | — | — | new |
| 3 | **Minor Conductive Hold** | 1 (**50 dust, bridge**) | Conductivity **and** Cane Edge (**All**) | `AbilityDuration` scope **`conductive_finisher_mark`**: finisher's non-disabling Conductive mark **1.5 s → 2.5 s**. Static Charge interrupt stays 0.4 s. **The resolver must never route this scope into stun duration.** Dormant until Lorentz Pulse unlocks. | Special 2 | `tesla_p2` **StatusDuration** 0.15 ("Minor Stun Duration +15%") | ⚠ retired key → new scoped key |
| 4 | Minor Coil Duration | 2 | Conductivity (All) | `AbilityDuration` scope `tesla_tesla_coil`: **30 s → 40 s** (flat +10 s, not +10%) | Special 1 | `tesla_c1` PersistentDuration 0.1 (**T1, 50 dust**) | tier + value + key |
| 5 | Minor Pulse Radius | 2 | Cane Edge (All) | `AbilityRange` +15% scope `tesla_lorentz_pulse` | Special 2 | `tesla_c2` PersistentRange 0.15 | scope |
| 6 | **Long Blink** ★ | 2 | Conductive Hold (All) | flag: Lightning Blink travels **+0.5 units** | Movement | — | **new node + new behaviour** |
| 7 | **Resonant Overdrive** | M | Coil Duration (All) | arcs **25% faster**; **coil arcs also target Chronal Extractors in radius** ★. The V6 "+5 s coil duration" clause is **removed** (moved to the minor). | Special 1 | `tesla_c3` — coil `+5 s` and faster arcs | **−duration, +extractor targeting** |
| 8 | **Lorentz Attraction** | M | Pulse Radius (All) | pull-then-root; Root **+0.5 s** (was +1 s). Root is a control status, outside the Stagger budget; Armored Recovery still ignores it. | Special 2 | `tesla_p3` — `AttractionRootBonusSeconds = 1f` | constant 1.0 → 0.5 |
| 9 | **Wardenclyffe Shield** | M | Long Blink (All) | **D02d Option A:** protection **and** recharge require being inside an **owned active coil's radius**. One **15% max-HP** pool. After actual shield-or-HP damage wait **3 live seconds**, then recharge **2.5% max HP / live second** while in range (6 s empty→full). Outside range both stop; stored charge stays dormant. No instant refill, no stacked rate from overlap/re-entry. Delay advances during live play even out of range; an earlier-layer absorb or a damage-free block does **not** restart it. **Starts at zero charge.** Same-attempt death/load clears charge but preserves remaining delay. | Special 1 | `tesla_w3` — always `ConfigureStoryShield(0.15×MaxHP)`; recharges only near a coil at a placeholder rate; starts full-capacity-configured | **moderate rewrite** |

⚠ Retired: `tesla_p1` SpecialDamage, `tesla_p2` StatusDuration ("Stun Duration").

**⚠ Open question — node ID retention (F07).** The design instructs "Minor Conductive Hold replaces Minor
Static Hold, **retaining its save/node identifier**". **No node named "Minor Static Hold" exists in the repo.**
The nearest shipped node is `tesla_p2` ("Minor Stun Duration +15%", 75 dust, prereq `tesla_p1`) — wrong tier,
wrong cost, wrong prereqs. Since every other node ID changes anyway, recommend authoring a fresh ID
(`tesla_w1` bridge slot is natural) and recording the deviation, rather than forcing a mismatched legacy ID.

**⚠ Scope creep flag — F07 Conductive mark.** The whole Conductive-mark system (separate 1.5 s mark, per
target *and* source Tesla, greater-of reapplication never additive, coil-fence marks at 1.5 s not extended by
the node, no marks from solo-coil arcs or chain follow-ups, consume-on-Lorentz with at-most-one chain per
execution per target, ticks only on live gameplay, cleared on target death/respawn or source Tesla
death/departure, saved/snapshotted with remaining time + ownership + same-execution chain state) **does not
exist**. Today `TeslaLorentzPulse.EmitPulse` reads `TargetHasStaticCharge(hurtbox)` and the finisher's Static
Charge is 0.4 s in `BasicComboRules.StringProfiles["tesla"]`. This is a **Large** kit item that the grid node
merely modifies — it is the biggest hidden cost in this section.

Cheapest first Major: **325** (Resonant Overdrive).

---

### 6.5 Shakespeare — *Three Acts* (tiers, not branches; F08 Option B)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Quill Reach (Act I Tragedy) | 1 | — | `AttackRange` +10% | — | `shakespeare_t1` AttackRange 0.1 | identical |
| 2 | Minor Wit (Act I Comedy) | 1 | — | `MoveSpeed` +3% | — | `shakespeare_c2` MoveSpeed 0.03 | prereq only |
| 3 | Minor Chronicle (Act I History) | 1 | — | `MaxHP` +15 | — | `shakespeare_h2` MaxHP 15 | prereq only |
| 4 | Minor Lament Slow (Act II) | 2 | **Any-of** [Quill Reach, Wit, Chronicle] | `AbilityDuration` scope `shakespeare_yoricks_lament`: Time Dilation **2.5 s → 3.0 s** | Special 1 | — | new |
| 5 | **Tempest Apex Jump** ★ (Act II) | 2 | **Any-of** [T1, C1, H1] | flag: The Tempest's lift can be **jumped from at its apex** (a jump refresh). *V7.6 correction — the V7 text said "barrier can be jumped from"; no barrier exists in his kit.* | Special 2 | — | **new node + new behaviour** |
| 6 | Minor Chronicle Rally (Act II) | 2 | **Any-of** [T1, C1, H1] | `RallyEchoFraction` +10% | — | — | **new lane** |
| 7 | **Macbeth's Curse** (Act III) | M | **All-of** [Lament Slow, Tempest Apex Jump, Chronicle Rally] | Lament also applies `Venom` (chip / 1.0 s for 3 s) on the damage slot, stacking with the wave's Time Dilation | Special 1 | `shakespeare_t3` | effect unchanged; **prereq All-of-3** |
| 8 | **Midsummer Glide** (Act III) | M | **All-of** [T2, C2, H2] | Prospero's Flight deals **8.0** to glided-through enemies, **+20%** glide speed. Story-only; **baseline Flight gains neither** | Movement | `shakespeare_c3` | effect unchanged; prereq |
| 9 | **Henry's Bastion** (Act III) | M | **All-of** [T2, C2, H2] | phantom guard absorbing **10% max HP**. **D01 Option B:** only a *real* block (after temporary protection) summons it, and the new guard **cannot absorb its triggering hit**; existing protection that fully absorbs a hit spends no charge and cannot trigger/refresh it. **D02a:** a new qualifying block **refills to cap and restarts duration — never stacks**; duplicate callbacks grant nothing. **D02c:** expires after **8 live seconds** or depletion; a valid repeat resets to 8 s. Existing effect-timer pauses apply. | — | `BlockSystem.GrantHenrysBastion` (L127–133) — grants fully charged on every successful block, **no timer**, no D01 ordering | **moderate rewrite** |

⚠ Retired: `shakespeare_t2` SpecialDamage, `shakespeare_c1` KnockbackForce ("Wind Pushback"),
`shakespeare_h1` **BlockDurability** ("Block Health").
Cheapest first Major: **475** (50 + 3×75 + 200) — the roster's latest peak, intentional.
Two Majors 675; three 875; all nine 975.
**F08 migration:** pre-F08 saves with a purchased Major missing a new Act II prerequisite get one atomic,
versioned, free respec — remove purchased nodes/effects, refund exactly the recorded paid costs once, preserve
campaign progress and undeposited earnings, **never grant missing nodes for free**.

---

### 6.6 Mozart — *The Ascending Scale* (linear six-note scale, three harmonics)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| 1 | Minor Tempo (Do) | 1 | — | `MoveSpeed` +3% | — | `mozart_a1` MoveSpeed 0.03 | identical |
| 2 | Minor Baton (Re) | 1 | Tempo (All) | `BasicAttackDamage` +5% | — | — | new |
| 3 | Minor Resonance (Mi) | 1 | Baton (All) | `UltimateBuildRate` **+10%** | — | — | **new lane** |
| 4 | Minor Chord Damage (Fa) | 2 | Resonance (All) | `AbilityDamage` +10% scope `mozart_requiem_chord` | Special 1 | `mozart_f2` SpecialDamage 0.1 | scope |
| 5 | **Extra Note** ★ (Sol) | 2 | Chord Damage (All) | flag: **two** Sonata Drift staff platforms may stand at once | Movement | — | **new node + new behaviour** |
| 6 | Minor Wave Cooldown (La) | 2 | Extra Note (All) | `CooldownReduction` −10% scope `mozart_fortissimo_wave` | Special 2 | `mozart_p2` CooldownReduction (character-wide) | scope |
| 7 | **Rest Shield** (the Third) | M | Minor Resonance (All) | still/blocking 1.5 s raises a silent bubble absorbing **physical projectiles** until Mozart moves or attacks. **Reachable at 350 dust — his early defensive answer.** | — | `mozart_p3` | **unchanged** |
| 8 | **Virtuoso Drift** (the Fifth) | M | Extra Note (All) | *(renamed from Virtuoso **Dash** — the universal dash was removed 2026-08-09)*. Standing on a staff platform grants **+20% move speed** and **immunity to ranged projectiles**; landing on one still refunds half of Sonata Drift's cooldown (baseline V7 rule, **not implemented**). | Movement | `mozart_a3` key `virtuoso_dash` | display rename; keep perk key |
| 9 | **Requiem Crescendo** (the Octave) | M | Wave Cooldown (All) | Requiem Chord detonates **twice**, second wider shockwave at 50% | Special 1 | `mozart_f3` | **unchanged** |

⚠ Retired: `mozart_a2` PersistentDuration ("Drift Duration"), `mozart_f1` ProjectileSpeed,
`mozart_p1` MaxHP +15 (dropped — Mozart has **no** MaxHP minor in V7.6).
Cheapest first Major: **350** (Rest Shield). Virtuoso Drift 500; Requiem Crescendo 575.
**Note:** Mozart's grid is the only one with **no `MaxHP` node** and **no root choice** other than Do.

---

### 6.7 Cleopatra — *The Delta* (three streams; every T2 needs a **pair** of T1s)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| A | Minor Asp Mark | 1 | — | `AbilityDamage` scope **`finisher_venom`**: the basic finisher's Venom mark ticks at **0.75** intensity instead of **0.5** | — | — | **new; touches a shared rulebook** |
| B | Minor Quickstep | 1 | — | `MoveSpeed` +3% | — | — | new |
| C | Minor Ptolemaic Vigor | 1 | — | `MaxHP` +15 | — | `cleopatra_pw1` MaxHP 15 | identical |
| X | Minor Nest Duration | 2 | **A and B** (All) | `AbilityDuration` +20% scope `cleopatra_serpent_nest` | Special 1 | `cleopatra_dm2` ZoneDuration 0.2 (vortex) | re-scoped to the **nest** |
| Y | **Vortex Step** ★ | 2 | **B and C** (All), **75 dust** | flag (F06 Option A): activating **Desert Mirage while inside her own Sandstorm Vortex** sets that cast's cooldown to **50%** of its otherwise-applicable value, *after* other modifiers (base 5 s → **2.5 s**). Cooldown starts at the normal activation point; never halve remaining time repeatedly; does not shorten the animation. **Once per vortex instance** — consume the allowance atomically on an accepted cast even if later interrupted; a refused input spends nothing; overlapping vortices cannot stack or consume more than one (pick deterministically); entering after Mirage begins does not qualify. The flag survives restoration of that same vortex; a despawned vortex supplies no saved charge; a new vortex has a fresh allowance. Does not extend vortex lifetime, affect other characters' zones, or retrigger on-cast effects. Respec removes future eligibility without resetting consumed allowances or rewriting a started cooldown. | Special 2 | — | **new node + new behaviour** |
| Z | Minor Venom Damage | 2 | **A and C** (All) | `AbilityDamage` +15% scope `venom` (all Venom ticks) | Special 1 | `cleopatra_al2` StatusDamage 0.15 | key rename |
| MA | **Asp's Bite** | M | Z (All) | Venom ticks deal **double damage to airborne enemies** | *(unspecified — recommend ungated: the finisher Venom is a Level 0 system)* | `cleopatra_al3` | **unchanged** |
| MQ | **Quicksand Grip** | M | Y (All) | enemies standing in a Sandstorm Vortex are **rooted 1 s** when she casts Desert Mirage. Needs Special 2 **and** Movement → author `gatedAbilityID = Special2` (the Level 3 milestone implies the Level 1 Movement unlock) | Special 2 | `cleopatra_dm3` | **unchanged** |
| MR | **Royal Aegis** | M | X (All) | Desert Mirage grants a sand shield absorbing **10% max HP**; **her sand decoy inherits it as a separate recipient**. **D02a:** a valid new grant refills the recipient's *same* shield to cap and restarts duration — no added capacity, no stacked copies, nothing from duplicate callbacks. **D02c:** expires after **8 live seconds** or depletion; valid repeat grants reset to 8 s. | Movement | `CleopatraAbilities.cs:327` — grants on Mirage, no timer, no decoy | **moderate rewrite; the sand decoy does not exist** |

⚠ Retired: `cleopatra_al1` BasicAttackDamage 0.08 (folded into the finisher-Venom node),
`cleopatra_dm1` ZoneRadius, `cleopatra_pw2` **BlockDurability**.
Cheapest first Major: **375** (50+50+75+200).
**⚠ Same ID-retention ambiguity as Tesla:** F06 says Vortex Step "replaces **Sand Walker** … keeping its
B + C prerequisites … retain the node's existing save identifier". **No node named "Sand Walker" is shipped.**
Author a fresh ID and record the deviation.
**⚠ Shared-rulebook hazard:** Minor Asp Mark scales `BasicComboRules.StringProfiles["cleopatra"]
.finisherStatusIntensityMilli = 500`, which the **Fighter sim also reads**. The Story multiplier must be
applied at the hit-application site in `PlayerController`, never by editing the rulebook constant.

---

### 6.8 Lincoln — *The Split-Rail Fence* (one post, then two rails and a crossbar)

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| R0 | **Minor Iron Frame (the post)** | 1 | — (**the only root node**) | `MaxHP` +25 | — | `lincoln_s1` MaxHP 25 | identical value |
| A1 | Minor Rail Reach | 1 | Iron Frame (All) | `AttackRange` +10% | — | `lincoln_l1` AttackRange 0.1 | prereq |
| B1 | Minor Wrestler's Rally | 1 | Iron Frame (All) | `RallyEchoFraction` **+15%** — the roster's largest | — | — | **new lane** |
| A2 | Minor Shockwave Damage | 2 | Rail Reach (All) | `AbilityDamage` +15% scope `lincoln_emancipator` | Special 1 | `lincoln_l2` SpecialDamage 0.15 | scope |
| B2 | **Rail Breaker** ★ | 2 | Wrestler's Rally (All), **75 dust** | flag (F06 Option A): each Rail Charge activation can destroy **one hostile projectile authored as breakable** during travel. Resolve the break **before** the projectile applies damage or on-hit effects. Consume the allowance **once per activation**, never per frame or per overlap. Later projectiles and unbreakable attacks resolve normally against his existing armor. Beams, persistent zones, environmental hazards and unbreakable-authored projectiles are **not** destroyed. No reflection, no extra damage, no blanket invulnerability, **no effect on the universal roll**. Baseline Rail Charge does **not** break projectiles. | Movement | — | **new node + new behaviour; needs a "breakable" projectile flag that does not exist** |
| C | Minor Splitting Cooldown (crossbar) | 2 | Iron Frame (All) | `CooldownReduction` −10% scope `lincoln_splitting_strike` | Special 2 | `lincoln_r2` CooldownReduction (character-wide) | scope; **T2 off a T1** |
| MA | **Executive Order** | M | Shockwave Damage (All) | shockwave travels **+50%** and deals **+20%** | Special 1 | `lincoln_l3` | **unchanged** |
| MB | **Homestead Bulwark** | M | Rail Breaker (All) | a Rail Charge that connects grants **3 s hyper-armor** | Movement | `lincoln_s3` | **unchanged** |
| MC | **Kinetic Splitting** | M | Splitting Cooldown (All) | **FULL REWORK** (resolves design-analysis 1.3 — the V6 perk restated the baseline and granted nothing). Now: Splitting Strike's spike **ground-bounces** an airborne target into a **20-frame follow-up window** (a true combo into the string's launching Hit 2 — **PvE only**, and subject to the Stagger Discipline's diminishing special stun), **and** the strike deals **+50% to Chronal Extractors and enemy constructs**. | Special 2 | `PlayerController.cs:2377` — makes combo hit 3 `AttackClass.Special` | **rewrite; nothing survives** |

⚠ Retired: `lincoln_s2` **BlockDurability** ("Block Health +15%"), `lincoln_r1` MoveSpeed.
Cheapest first Major: **325** (Kinetic Splitting). Executive Order 375; Homestead Bulwark 375.

---

### 6.9 Pocahontas — *Crossing Currents* (two currents crossing, one Spirit star apart)

> *Design open decision (Part 4): the roster's Pocahontas slot may be swapped. The grid's **shape** survives a
> swap; its **node names** would not. Author names via translation keys so a rename is CSV-only.*

| # | Designed node | T | Prereqs (mode) | Stat / effect | Gate | Current | Delta |
|---|---|---|---|---|---|---|---|
| W1 | Minor Windstep (Wind) | 1 | — | `MoveSpeed` +3% | — | — | new |
| R1 | Minor Staff Edge (River) | 1 | — | `BasicAttackDamage` +8% | — | `pocahontas_fs1` BasicAttackDamage 0.08 | identical |
| S1 | Minor Forest Vigor (Spirit) | 1 | — | `MaxHP` +15 | — | `pocahontas_pw1` MaxHP 15 | identical |
| W2 | Minor Glide Duration | 2 | Windstep (All) | `AbilityDuration` +20% scope `pocahontas_breeze_glide` | Movement | `pocahontas_wr2` GlideDuration 0.2 | key rename only |
| R2 | Minor Snare Duration | 2 | Staff Edge (All) | `AbilityDuration` scope `pocahontas_vine_snare`: Root **1.5 s → 2.0 s** | Special 2 | — | new (base `StatusDuration = 1.5` in `special_2.tres` ✓) |
| X | **Second Glide** ★ (the crossing) | 2 | Windstep **or** Staff Edge (**Any**) | flag: Breeze Glide can be **re-entered once per airtime** | Movement | — | **new node + new behaviour** |
| MW | **Tornado Lift** | M | Glide Duration (All) | starting a Breeze Glide raises an updraft that **launches** nearby enemies | Movement | `pocahontas_wr3` | **unchanged** |
| MR | **Thorn Snare** | M | Snare Duration (All) | Rooted enemies take a **damage-slot DoT** for the Root's duration, stacking with the Root under the two-slot rule | Special 2 | `pocahontas_fs3` | **unchanged** |
| MS | **Leaf Barrier** | M | **All-of** [Second Glide, Forest Vigor] | entering a Breeze Glide grants a leaf shield absorbing **10% max HP**. **D02a:** each valid new entry — **including an accepted Second Glide re-entry** — refills the *same* shield to cap and restarts duration; no stacked copies, no additive capacity/duration; continued glide, held input and duplicate callbacks grant nothing. **D02c:** expires after **8 live seconds** or depletion; a valid entry resets to 8 s. | Movement | `PocahontasAbilities.cs:257` — no timer, no refill semantics | **moderate rewrite + new prereq shape** |

⚠ Retired: `pocahontas_fs2` SpecialDamage, `pocahontas_wr1` **JumpForce +15%** (jump-height minor),
`pocahontas_pw2` **BlockRecovery** ("Block Recovery +10%").
Cheapest first Major: **325** (Tornado Lift). Leaf Barrier 375.

---

## 7. Retired stat-key audit (authoritative list of dead nodes to delete)

| Retired key | Where authored today | Design ruling |
|---|---|---|
| `Armor` | `joan_d1` | Resolves neutral today (documented). "A damage-reducing armor stat is explicitly against the combat pillar." **Delete.** |
| `BlockDurability` ("Block Health") | `einstein_d2`, `shakespeare_h1`, `cleopatra_pw2`, `lincoln_s2` | Resolves neutral today. "No such stat exists." **Delete all four.** |
| `BlockRecovery` | `pocahontas_pw2` | "No such stat exists." Resolver lane exists but nothing consumes it meaningfully. **Delete node**; lane may stay. |
| `ComboSpeed` | `joan_m2` | Not used by any V7.6 node. **Delete node.** |
| `JumpForce` (jump-height minors) | `joan_r2`, `leonardo_m2`, `pocahontas_wr1` | "Retired roster-wide — the compressed jump spread makes weight and air control the identity axes, not jump force." **Delete all three.** |
| `StatusDuration` used for **stun** | `tesla_p2` ("Minor Stun Duration +15%") | V7.6 hygiene: "minors never touch stun or hitstun duration (the V7.4 Stagger Discipline owns those numbers)". Replaced by `AbilityDuration(conductive_finisher_mark)`. **Delete; resolver must refuse to route that scope into stun.** |
| "Dash Speed" | *never authored* — V6 named Joan R1 "Dash Speed"; the shipped node is `MoveSpeed` | Already dead (universal dash removed 2026-08-09). No action. |
| `SpecialDamage` (character-wide) | `einstein_o1`, `leonardo_a1`, `tesla_p1`, `shakespeare_t2`, `lincoln_l2`, `mozart_f2`, `pocahontas_fs2` | Replaced by ability-scoped `AbilityDamage`. Lane may stay for majors. |
| `ProjectileSpeed` | `einstein_o2`, `mozart_f1` | No V7.6 node uses it. **Delete nodes.** |
| `ProjectileDamage` | `leonardo_e2` | Folded into `ConstructHP`/ability scope. **Delete node.** |
| `GlideSpeed` | `leonardo_m1` | No V7.6 node. **Delete node.** |
| `KnockbackForce` | `shakespeare_c1` ("Wind Pushback") | No V7.6 node. **Delete node.** |

Also barred by the V7.6 hygiene rule (nothing authored today violates these, but the validator should enforce
them): minors may never grant **rewind charges**, and may never modify the **Timeline Integrity drain rate**.
Grids may only interact with the V7.6 timer indirectly — e.g. bonus damage to Chronal Extractors.

---

## 8. Bespoke code hooks per character — what exists, what is new

### 8.1 Already wired (do not rebuild)
All 27 Major perk keys exist as constants with live consumers:

| Character | Perk keys (all present) | Consumer file |
|---|---|---|
| Einstein | `event_horizon`, `critical_mass` / `quantum_entanglement` | `EinsteinEmc2Blast.cs` / `BlockSystem.cs:181` |
| Joan | `unstoppable_crusade` / `zealous_vigor`, `shield_of_orleans` | `JoanAbilities.cs:30,38` / `PlayerController.cs:567,720` |
| Leonardo | `master_stroke`, `clockwork_overdrive`, `daedalus_wings` | `LeonardoAbilities.cs` |
| Tesla | `resonant_overdrive`, `wardenclyffe_shield`, `lorentz_attraction` | `TeslaAbilities.cs` |
| Shakespeare | `macbeths_curse`, `midsummer_glide` / `henrys_bastion` | `ShakespeareAbilities.cs` / `BlockSystem.cs:117` |
| Mozart | `requiem_crescendo`, `rest_shield`, `virtuoso_dash` | `MozartAbilities.cs` |
| Cleopatra | `asps_bite`, `quicksand_grip`, `royal_aegis` | `CleopatraAbilities.cs` |
| Lincoln | `executive_order`, `homestead_bulwark`, `kinetic_splitting` | `LincolnAbilities.cs` / `PlayerController.cs:2377` |
| Pocahontas | `thorn_snare`, `tornado_lift`, `leaf_barrier` | `PocahontasAbilities.cs` |

### 8.2 Majors needing code work

| Character | Major | Work | Size |
|---|---|---|---|
| Einstein | Event Horizon | add "projectiles crossing the rift slowed to half speed" | S |
| Einstein | Critical Mass | Radiant Burn = 1.25× vulnerability, **3 s**, **no periodic damage**; apply *after* the triggering hit resolves; skip projectile-contact / blocked / invulnerable; damage-slot stronger-wins; coexists with rift Time Dilation | S–M |
| Einstein | Quantum Entanglement | verify warp resolves **before** the daze; keep 5 s lockout | S |
| **Joan** | **Zealous Vigor** | **rewrite**: extra 25% Rally-echo reclaim instead of 5%-missing-HP heal | M |
| **Joan** | **Shield of Orléans** | **rewrite**: flat +5 meter per *distinct* blocked attack (**attack-ID dedup, per-execution**), plus Guard-Crush one-charge refund with lockout-aware unusability | **L** |
| Leonardo | Clockwork Overdrive | bolts ×2 damage vs Chronal Extractors | S |
| Tesla | Resonant Overdrive | drop the +5 s clause; **coil arcs also target Extractors** (new target acquisition against `damageable_environment`) | M |
| Tesla | Lorentz Attraction | `AttractionRootBonusSeconds` 1.0 → 0.5 | XS |
| **Tesla** | **Wardenclyffe Shield** | **D02d rewrite**: in-range-only protection *and* recharge, 3 s post-damage delay that advances out of range, 2.5%/s, dormant charge, zero at start, death/load clears charge but keeps delay | M |
| Shakespeare | Henry's Bastion | **D01 + D02a + D02c**: real-block-only trigger, cannot absorb its trigger, refill-not-stack, **8 s timer** | M |
| Mozart | Virtuoso Drift | display rename only (keep `virtuoso_dash` key); the "landing refunds half cooldown" baseline rule is unimplemented | S |
| Cleopatra | Royal Aegis | D02a + D02c; **sand decoy as a separate recipient** — the decoy itself does not exist | M |
| **Lincoln** | **Kinetic Splitting** | **full rewrite**: ground-bounce into a 20-frame follow-up window (PvE only, subject to diminishing special stun) + **+50% vs Extractors and enemy constructs** | **L** |
| Pocahontas | Leaf Barrier | D02a + D02c; refill on accepted Second Glide re-entry | M |

### 8.3 Traversal flags — all nine are new behaviour

| Character | Flag | Implementation site | Size | Notes |
|---|---|---|---|---|
| Einstein | Extended Float (+20 f) | `EinsteinRelativityWarp` float window | S | float window is frame-authored in `movement.tres` |
| **Joan** | Wings Refresh | `PlayerController` combo-3 hit confirm + `JoanRighteousSmite` on-hit; reset `joan_ascendant_wings` cooldown | M | once-per-execution dedup; must **not** restore air jumps or bypass Suppression/Time Freeze |
| Leonardo | Re-placement | `LeonardoTurretNode` pickup/re-place, once | M | interacts with `PoolManager` release |
| Tesla | Long Blink (+0.5 units) | `tesla/movement.tres` distance at cast | S | |
| Shakespeare | Tempest Apex Jump | `ShakespeareTheTempest` lift apex → jump refresh | M | |
| Mozart | Extra Note | `MozartSonataDrift` platform cap 1 → 2 | S | deploy-limit already modelled for persistents |
| **Cleopatra** | Vortex Step | `CleopatraDesertMirage` cast + `SandstormVortex` per-instance allowance flag | **L** | atomic per-vortex consume, deterministic pick among overlaps, survives restoration, respec semantics |
| **Lincoln** | Rail Breaker | `LincolnRailCharge` travel vs projectiles | **L** | **requires a new "breakable" authoring flag on hostile projectiles**, which does not exist |
| Pocahontas | Second Glide | `PocahontasBreezeGlide` re-entry once per airtime | M | needs an airtime latch reset on ground/stock loss |

### 8.4 Kit-level (non-grid) changes inside this section's diff

| Item | Current | Design | Size |
|---|---|---|---|
| **F07 Conductive mark** (Tesla) | `TargetHasStaticCharge` gates Lorentz chains; finisher Static Charge 0.4 s in `BasicComboRules.StringProfiles["tesla"]`; coil fence applies 1.0 s Static Charge (`TeslaCoilNode.FenceStaticChargeDuration`) | Separate 1.5 s Conductive mark, per target **and** source Tesla, greater-of reapplication, fence marks at 1.5 s (not node-extended), no marks from solo arcs or chain follow-ups, one chain per target per Pulse execution, live-tick-only lifetime, cleared on death/respawn/source departure, save/snapshot state | **L** |
| **F15 block model** — Joan Divine Piercing | `JoanAbilities.cs:159` `DepleteCharges(BlockChargeDepletion)` (2) | **Consume all remaining charges** on a valid block (ordinary Special-class full shatter) | S |
| **F15 block model** — Lincoln Emancipator | `LincolnAbilities.cs:143` `DepleteCharges(2)` | Same — full shatter | S |
| **F15 Shakespeare movement** | `shakespeare_prosperos_flight` already authored; no teleport found | Confirm: no teleport, no forced facing swap, no 6-frame reappearance invulnerability; baseline gains no Midsummer bonuses | S (verify) |
| **D03g** Rally exclusions | Rally reclaim rides `ApplyHitPayload` for every landed hit | **Einstein Rift ticks and every Cleopatra Vortex tick (including the launching final tick) reclaim no Rally health** | S |
| **D03h** meter exclusions | `AddInfluenceFromDamageDealt` on all damage | Tesla's **Ultimate-triggered coil explosions** earn no meter (ordinary coil damage still does); Cleopatra's **Ultimate impacts and its lingering poison ticks** earn none | S |
| **C01a** comfort | — | With Reduced Temporal Effects on, suppress the Rift's lens/chromatic distortion; use a stable readable zone boundary; gameplay unchanged | S (coordinate with the comfort/settings agent) |
| Joan Ascendant Wings baseline | no finisher refresh implemented | correct by default (F06 Option C makes refresh node-only) | none |
| Lincoln Rail Charge baseline | no projectile break implemented | correct by default (F06 Option A) | none |
| Mozart Sonata Drift cooldown refund | not implemented | V7 baseline rule; still owed | S |
| Cleopatra Mirage sand decoy (1 s) | not implemented | V7 baseline rule; Royal Aegis depends on it | M |

---

## 9. `en.csv` key inventory

**Current:** 185 `resonance_*` rows = **162 node rows** (9 chars × 9 nodes × {`_name`, `_description`})
+ **23 UI rows**.

**UI rows — all survive unchanged:**
`resonance_title`, `resonance_balance`, `resonance_cost`, `resonance_unlocked`, `resonance_confirm_title`,
`resonance_confirm_unlock`, `resonance_confirm_ok`, `resonance_confirm_cancel`, `resonance_grid_unavailable`,
`resonance_result_{unlocked,already,prerequisite,dust,unavailable}`, `resonance_locked_label`,
`resonance_state_{available,locked_prerequisite,locked_dust}`, `resonance_hint_controls`,
`resonance_respec_{button,title,confirm,done}`.

**New UI rows needed:**

| Key | Purpose |
|---|---|
| `resonance_state_locked_ability` | dormant/gated node ("Unlocks with …") |
| `resonance_dormant_node` | silhouette label for an unnamed dormant star |
| `resonance_state_locked_prerequisite_any` | "Locked — requires any of {0}" (Any-of mode) |
| `resonance_result_ability_locked` | new `ResonanceUnlockResult` |
| `resonance_tier_{1,2,3}` *(optional)* | tier chip on the node card |

**Node rows:** all 162 are replaced. Node IDs change in every grid (positions, tiers and effects all move), so
the safest scheme is to keep the `resonance_{character}_{nodeid}_{name|description}` convention with **new**
node IDs and **delete every old row**. Suggested ID scheme (stable, readable, survives a topology tweak):

| Character | New node IDs |
|---|---|
| einstein | `einstein_momentum`, `einstein_chalk_edge`, `einstein_mass`, `einstein_extended_float`, `einstein_rift_range`, `einstein_rally_resonance`, `einstein_event_horizon`, `einstein_critical_mass`, `einstein_quantum_entanglement` |
| joan | `joan_zeal`, `joan_vigor`, `joan_martyrs_resolve`, `joan_wings_refresh`, `joan_smite_damage`, `joan_piercing_cooldown`, `joan_unstoppable_crusade`, `joan_zealous_vigor`, `joan_shield_of_orleans` |
| leonardo | `leonardo_brush_reach`, `leonardo_compass_slam`, `leonardo_workshop_vigor`, `leonardo_spiral_range`, `leonardo_turret_plating`, `leonardo_replacement`, `leonardo_master_stroke`, `leonardo_clockwork_overdrive`, `leonardo_daedalus_wings` |
| tesla | `tesla_conductivity`, `tesla_cane_edge`, `tesla_conductive_hold`, `tesla_coil_duration`, `tesla_pulse_radius`, `tesla_long_blink`, `tesla_resonant_overdrive`, `tesla_lorentz_attraction`, `tesla_wardenclyffe_shield` |
| shakespeare | `shakespeare_quill_reach`, `shakespeare_wit`, `shakespeare_chronicle`, `shakespeare_lament_slow`, `shakespeare_tempest_apex`, `shakespeare_chronicle_rally`, `shakespeare_macbeths_curse`, `shakespeare_midsummer_glide`, `shakespeare_henrys_bastion` |
| mozart | `mozart_tempo`, `mozart_baton`, `mozart_resonance`, `mozart_chord_damage`, `mozart_extra_note`, `mozart_wave_cooldown`, `mozart_rest_shield`, `mozart_virtuoso_drift`, `mozart_requiem_crescendo` |
| cleopatra | `cleopatra_asp_mark`, `cleopatra_quickstep`, `cleopatra_ptolemaic_vigor`, `cleopatra_nest_duration`, `cleopatra_vortex_step`, `cleopatra_venom_damage`, `cleopatra_asps_bite`, `cleopatra_quicksand_grip`, `cleopatra_royal_aegis` |
| lincoln | `lincoln_iron_frame`, `lincoln_rail_reach`, `lincoln_wrestlers_rally`, `lincoln_shockwave_damage`, `lincoln_rail_breaker`, `lincoln_splitting_cooldown`, `lincoln_executive_order`, `lincoln_homestead_bulwark`, `lincoln_kinetic_splitting` |
| pocahontas | `pocahontas_windstep`, `pocahontas_staff_edge`, `pocahontas_forest_vigor`, `pocahontas_glide_duration`, `pocahontas_snare_duration`, `pocahontas_second_glide`, `pocahontas_tornado_lift`, `pocahontas_thorn_snare`, `pocahontas_leaf_barrier` |

⚠ **`UnusedTranslationKeyTests`** (`RecordedOrphanCeiling = 11`) fails on any *new* orphan. Deleting the 162
old rows in the same commit as the rewrite keeps it green; leaving them in adds 162 orphans and fails hard.
The sweep resolves keys through the **compiled** `en.en.translation`, so run
`--headless --path … --import` and commit the regenerated `localization/en.en.translation` alongside `en.csv`.

⚠ Two existing description rows carry pre-existing content bugs worth fixing in the rewrite:
`resonance_leonardo_e3_description` says "five … instead of **three**" (design: 4→5), and
`resonance_joan_d3_description` / `resonance_joan_m3_description` describe the retired V6 effects.

---

## 10. Tests that pin the current grids (rewrite list)

| File | Test | What breaks | Action |
|---|---|---|---|
| `tests/unit/ResonanceProgressionTests.cs` (412 L, 12 cases) | `AllCharacterGridsHaveThreeValidThreeNodeBranchesAndLocalizedCopy` | Asserts `Nodes.Length == 9` ✓, but also **T1 ⇔ zero prerequisites** (breaks in Joan/Leonardo/Tesla/Lincoln), **Major has exactly 1 prerequisite** (breaks for Leonardo capstone, all 3 Shakespeare Majors, Pocahontas Leaf Barrier), **T2 has exactly 1 prerequisite** (breaks for every Any-of / pair node) | **Full rewrite** → assert explicit tiers, costs 50/75/200, total 975, prerequisiteMode validity, no cycles, no dangling IDs, and per-character expected topology |
| same | `EveryAuthoredMinorNodeKeyEitherResolvesOrIsDocumentedUnresolved` | `documentedUnresolvedKeys = {BlockDurability, Armor}` must be **emptied**; `resolvedKeys` gains `RallyEchoFraction`, `UltimateBuildRate`, `ExtractorDamage`, `AbilityDamage`, `AbilityRange`, `AbilityDuration`, `ConstructHP`; traversal nodes carry **no** stat key (the test currently requires every Minor to have one) | rewrite |
| same | `RePointedTalentMinorsCarryTheirDesignedAbilityScopedKeys` | Pins `einstein_u2`/`leonardo_a2`/`cleopatra_dm1`/`cleopatra_dm2`/`pocahontas_wr2` by ID + key | rewrite to new IDs + scoped keys |
| same | `UnresolvedMinorKeysAndLockedNodesResolveNeutral` | Built on `BlockDurability`/`Armor` synthetic nodes | rewrite (keep the locked-node-contributes-nothing half) |
| same | `MinorCooldownPersistentAndStatusKeysResolveFromAuthoredGrid` | Loads `tesla_grid` and pins `tesla_c1/c2/p2/w2` values | rewrite |
| same | `MinorRangeComboKnockbackProjectileGlideAndRecoveryKeysResolve` | Synthetic grid over 12 lanes incl. retired ones | rewrite / trim |
| same | `UnlockedGenericAndMajorModifiersRemainCharacterScoped` | `tesla_c1/c2/c3` IDs | ID update |
| same | `CollectUnlockedAbilityModifiersReturnsOnlyUnlockedMajorPerkKeys` | `einstein_u1/u2/u3/o1` IDs; also the **name is now wrong** — traversal flags are non-Major nodes that carry an `AbilityModifierKey` | rewrite + rename |
| same | `UnlockRequiresDustAndAllPrerequisites`, `EvaluateUnlockReportsBlockingReasonWithoutMutatingSave`, `ResolvedModifiersAreStoryOnlyValues`, `GridCannotSpendAnotherCampaignCharactersDust`, `UnlockedProgressSurvivesSaveEnvelopeRoundTrip`, `PlayerPerkQueryIsEmptyUnlessStoryProgressionPopulatesIt` | synthetic `BuildGrid()` + `einstein_d1/d2` IDs | minor ID updates |
| **NEW** | — | Any-of prerequisite satisfaction; Shakespeare 475-dust cheapest-Major arithmetic; per-character cheapest-route table; Leonardo capstone All-of-3; cycle rejection; gate-locked result | **add** |
| `tests/unit/ResonanceGridSceneTests.cs` (136 L, 5) | `TheAuthoredSceneCarriesEveryControlThePanelBindsByPath` pins `NodeGrid` as a `GridContainer` | **rewrite** when the container type changes |
| same | `TheNodeGridIsPopulatedFromTheActiveCharacterGridAndStartsOnTheFirstNode` | row-major assumption | rewrite |
| `tests/unit/ResonanceGridNavigationTests.cs` (47 L, 3) | Row-major clamp over a 3-column grid | **retire or rewrite** for adjacency navigation |
| `tests/unit/ResonanceRespecTests.cs` (98 L, 4) | Uses real grid IDs for spend/refund | ID updates only; add the **F08 migration respec** case |
| `tests/unit/AbilityZoneTalentTests.cs` (4 cases) | Pins `pocahontas_wr2` glide, `einstein_u2` rift radius, `cleopatra_dm1/dm2` vortex, `leonardo_a2` spiral — all by **node ID and generic lane** | **rewrite to scoped keys**; Cleopatra's nest/vortex re-scope changes the asserted target |
| `tests/ContentValidation/LincolnContentTests.cs:59` | `LincolnGridAuthorsTheThreeMajorPerkKeysTheKitConsumes` | perk keys survive; node IDs change | small update; **consider generalising to all nine characters** |
| `tests/unit/MirrorParadoxTests.cs:140` | `GridProgress["einstein"] = {einstein_u1, u2, u3, o1, d1}` isolation fixture | ID update only |
| `tests/ContentValidation/UnusedTranslationKeyTests.cs` | orphan ceiling 11 | must stay green — delete old rows in the same commit |
| `tests/ContentValidation/ContentManifestTests.cs` | 9 `ResonanceGrid` rows + `UIScreen,resonance_grid` | no change unless statuses move |
| `tests/ContentValidation/ScriptTypedEmptyArrayTests.cs` | **Hazard, not a break:** every grid ends with `Nodes = Array[ExtResource("2")]([...])`. **Never author an empty one.** | heed |

Rough test delta: **−3 rewritten-in-place, +8 to +14 new cases** on top of the 1638 baseline. Every grid change
touches `AuthoredResources`-cached resources, so a full `dotnet test` run is mandatory (see CLAUDE.md failure
signatures — read the `Total:`, not the exit code).

---

## 11. Sizing and shared files

| Work item | Size | Shared files touched |
|---|---|---|
| `ResonanceNodeData` schema (+5 fields, enum, SchemaVersion 2) | **S** (~40 L) | `ResonanceNodeData.cs`, `ResonanceGridData.cs` |
| `EvaluateUnlock` Any-of + ability gate + validation | **S–M** (~60 L) | `ResonanceProgression.cs` |
| Scoped stat bucket in `StoryStatProfile` + `Resolve` + 3 new lanes | **M** (~150 L) | `ResonanceGridData.cs`, `ResonanceProgression.cs`, `CharacterFactory.cs`, `PlayerController.cs` |
| Re-author 9 `.tres` grids | **M** (~950 L of data, mechanical once the tables above are settled) | `resources/Resonance/*` |
| `en.csv` 162 rows replaced + 5 new UI keys + `--import` | **S–M** | `localization/en.csv`, `localization/en.en.translation` |
| Grid UI topology renderer (positions, edges, adjacency nav, tooltips) | **L** (~350 L + scene rework) | `ResonanceGridPanel.cs`, `ResonanceGridNavigation.cs`, `scenes/ui/ResonanceGrid.tscn` |
| 9 traversal behaviours | **L** total (Cleopatra + Lincoln are the hard two) | 8 files under `scripts/Characters/Abilities/`, `PlayerController.cs` |
| 8 Major reworks (2 full rewrites) | **L** total | `PlayerController.cs`, `BlockSystem.cs`, `TeslaAbilities.cs`, `LincolnAbilities.cs`, `CleopatraAbilities.cs`, `PocahontasAbilities.cs`, `EinsteinEmc2Blast.cs`, `LeonardoAbilities.cs` |
| F07 Conductive mark system | **L** standalone | `TeslaAbilities.cs`, `TeslaCoilNode.cs`, `StatusController`, `BasicComboRules` (read-only) |
| Save migration (drop-unknown-node + refund; F08 versioned respec) | **M** | `SaveManager.cs`, `SaveEnvelope.cs`, `ResonanceProgression.cs` |
| Test rewrites | **M–L** | 6 test files + ~10 new cases |

**Hot shared files — coordinate with other agents before editing:**
`scripts/Characters/PlayerController.cs` (Zealous Vigor, Shield of Orléans, Kinetic Splitting, Rally echo,
22 `Story*` fields), `scripts/Combat/BlockSystem.cs` (Henry's Bastion, Quantum Entanglement, `DepleteCharges`
— the combat/block agent almost certainly also owns this), `scripts/Combat/BasicComboRules.cs`
(**read-only for this section** — it is cross-mode), `scripts/Characters/CharacterFactory.cs`,
`localization/en.csv`, `resources/Content/content_manifest.csv`.

---

## 12. Open questions / conflicts to escalate

1. **`gatedAbilityID` has no upstream.** No Legacy Unlock Schedule exists in the repo (`grep -r LegacyUnlock`
   → 0). Every V7.6 grid depends on it for dormant silhouettes and for the "root-adjacent nodes modify a
   Level 0 system" rule. Author the field; gate rendering behind a feature flag until §3 lands.
2. **Node-ID retention instructions reference nodes that were never shipped.** F07's "Minor Static Hold" and
   F06's "Sand Walker" do not exist in `resources/Resonance/`. Recommend fresh IDs + a deviation-ledger entry.
3. **Layout coordinates are absent from the design's schema snippet** but are mandatory for nine unique
   topologies. Proposing `[Export] Vector2 LayoutPosition` on `ResonanceNodeData`.
4. **Asp's Bite gate unspecified.** Every other Major names a gate. Her finisher-Venom is a Level 0 system, so
   ungated is defensible; confirm.
5. **Tier is not derivable from prerequisites any more** (four grids break the current inference) and only
   partly from cost. Recommend an explicit `Tier` export.
6. **`ExtractorDamage` is declared in the design's `StatType` enum but no authored minor uses it.** The three
   extractor effects are Majors. Confirm it is future-proofing, not a missing node.
7. **`StatModifierIsPercent` is decorative** — `PercentValue()` returns the same value either way. Do not
   "fix" this without auditing all 54 authored values.
8. **Mozart has no `MaxHP` minor** and **Cleopatra has no flat basic-damage minor** in V7.6. Both look
   deliberate (scale/delta identity) but are worth a one-line confirm before authoring.
9. **`DESIGN_BUILD_DEVIATIONS.md` records zero confirmed deviations** and explicitly states no build evidence
   was available in the docs workspace. Every finding in this dossier is new evidence against
   commit `31fed14` and belongs in that ledger as confirmed-open entries.
