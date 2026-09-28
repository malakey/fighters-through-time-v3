# Narrative resolution decisions

Design revision: 2026-09-12. N01 is resolved by user-selected **Option B: after the boss, the player interacts with a marked anchor to seal the timeline and trigger the restoration/completion sequence**. N02 is resolved by user-selected **Option A: sealing permanently repairs a specific historical moment/outcome; 4A addresses the separate unresolved nexus wound in the same era**. N03 is resolved by user-selected **Option A: brief recognition in the shared home-era level, with the larger personal story reserved for 4A**. N04 is resolved by user-selected **Option A: keep named captive/release scenes, selected from the non-active roster, and rescue only the chosen hero in the opening**. This specifies design behavior; scene authoring, implementation and runtime validation remain pending.

Authority: [main design](../design-godot-v7.md), [Story persistence](STORY_PERSISTENCE.md), [dust economy](DUST_ECONOMY.md), [comfort settings](COMFORT_SETTINGS.md) and [temporal rules](TEMPORAL_STATE_CONTRACT.md).

## N01 — Player-triggered sealing after the boss

For campaign boss levels, including each 4A, boss defeat unlocks a sealing interaction rather than immediately completing the level. Level 0 keeps its existing tutorial transition. The repeated sequence is: defeat the boss → existing brief defeat dialogue → regain control beside the boss reward and clearly marked sealing anchor → use Interact → restoration vignette/results and the existing onward destination.

Show the current C01c binding with **“Seal Timeline — Complete Level.”** Use a single deliberate Interact press, without a new hold/channel, meter cost, consumable, purchased perk, combat move or added confirmation dialog. This is the normal completion action, not another puzzle or a search for remaining Extractors.

### Availability, placement and rewards

Activate only after the required boss defeat has committed and the defeat presentation has finished. Use normal alive, actionable and interaction-range checks. Time Freeze, death/recovery and paused presentation states cannot trigger or queue sealing. No remaining optional Extractor, secret or upgrade is required. Register a stable level/anchor ID, distinct from checkpoint and Nexus-source IDs; this anchor grants no checkpoint Mending, rewind refill or Integrity benefit.

Place it beside the existing boss-reward/exit area on stable, clear ground reachable with every eligible kit and depleted resources. Do not require returning across the level, a paid ability, another fight or a pit crossing. Keep the anchor and pending boss pickup accessible from the pre-boss recovery baseline if the player reloads before sealing. The interaction grants no new invulnerability or healing.

The boss's existing **25-dust Large physical pickup** still spawns once at the arena center and can be collected before the player deliberately completes the level. Do not replace it with a wallet-direct award or auto-collect it on sealing. The completion transaction includes only actually collected/retained dust and the existing tier bonus; uncollected rewards are not silently credited. Sealing adds no new loot and no mandatory cleanup objective.

### What the sealing shows

Use the existing in-engine victory/exit vignette to show the anchor settling, local rifts closing, remaining local siphons powering down, and the interrupted historical outcome resuming. Tie the era-specific image/dialogue to that level's sabotage. Keep the current Restored/Stabilized/Fractured variant selected from final Integrity; a Fractured result still completes/seals the objective rather than leaving the level replayable.

Distinguish **shutdown by sealing** from **destruction earned during play**. Remaining Extractors/conduits receive a terminal sealed/inactive state, not a destruction event, pickup, secret claim, kill count or damage/meter reward. Preserve previously collected claims and destruction facts. Do not recompute final Integrity, refund time, lower the fixed starting-Extractor denominator or upgrade the earned tier when remaining machines shut down.

The pre-boss clock is already locked: time spent collecting the boss pickup or approaching the sealing anchor cannot change the recorded score. Preserve existing reporting rules for completion time; it remains informational rather than another grade. No new timer or failure challenge starts after the boss.

### Act III and ending boundaries

Acts I–II retain the results/deposit/Time-Ship route. Levels 13–14 retain the existing no-hub transition to the next level and completion-only Beacon deposit. Use their authored local objective to express the same interaction: securing the passage or disabling the remaining local intake. Do not imply that the Forge, every captive or the firing channel is released at those earlier endpoints.

At Level 14, the interaction completes the local objective and triggers the authored firing/escape-to-Level-15 sequence; the firing channel remains the route to the Meridian. At Level 15, defeating The First Unbound exposes the half-seated Prime Anchor for the player's sealing interaction. That accepted interaction triggers its shattering and the existing total-release ending. It adds no second boss phase or attack test. Preserve the final campaign-average ending variant, all captives' release and the hero's eventual return. Commit completion/rewards before credits, not when credits finish.

### Persistence and event ordering

Represent `AwaitingSeal` using the committed boss-defeated fact, ready anchor ID and pending physical pickup state. Boss defeat commits those facts once; it does not grant level completion, unlocks or deposit. Loading this state reconstructs a defeated boss, an accessible ready anchor and at most one pending uncollected reward, retaining the current F10 resources and locked score. Normal live-attempt death/quit/recovery rules still apply; readiness is not a new checkpoint or free resource recovery.

On accepted Interact, commit a unique sealing/completion transaction with the existing retained-base/bonus/deposit, locked final Integrity, sealed/inactive local-siphon result, completed-level marker, unlocks and destination. Commit before starting the completion vignette/transition. Solve the interaction once; duplicate presses, callbacks, visual effects and repeated loads cannot settle it again. Results/Auto-Deposit screens present the committed result, not another payout.

After that commit, skip/watch/interruption follows F10's existing completion-presentation rules: resume the unfinished presentation or its onward flow without requiring another seal, reviving the boss, reopening the level or replaying any reward. Before commit, reload retains AwaitingSeal and asks for the player's interaction. A failed write follows existing save-failure handling and cannot falsely acknowledge a completed level.

Use distinct boss-defeat, seal-ready, seal-accepted and presentation event identities; a defeat animation finishing must not still call the old automatic completion path. Apply C01a reduced visuals, C01b audio clarity and existing pause/skip handling without changing the gameplay transaction.

## Pending verification

Verify every boss level and 4A on all difficulties; ready interaction with empty meter/spent healing/unavailable cooldowns, C01c remaps, Time Freeze rejection and safe anchor/pickup access. Check uncollected versus collected boss rewards, all/none/some optional Extractors destroyed, no shutdown loot or secret credit and identical final Integrity/tier before and after sealing.

Exercise quit/crash before boss defeat commit, AwaitingSeal, pickup collection, seal commit, vignette skip/interruption, results/deposit and final credits. Confirm one boss reward, one completion/deposit, preserved locked score and correct Act III/no-hub and final-ending destinations. Validate actual era-specific restoration imagery and reduced presentation; no runtime passes are claimed.

## N02 — Sealed moments stay sealed

User selected Option A on 2026-09-12. **Sealing protects the particular repaired historical moment and its outcome, not every place, person and event in an entire era.** Replace the absolute rule “sealed eras stay sealed” with **“sealed moments stay sealed.”** The campaign still has one mutable timeline; this introduces no alternate branch, time-loop replay or chronal-pocket version of a Legacy Level.

A successfully sealed mission's repaired outcome remains permanent within the campaign. A later incursion elsewhere in that era cannot reopen the same sealed wound, reverse its victory, reset its completion/rewards or demand that the player complete it again. “Restore an era” may remain concise mission language only when its context clearly means that mission's historical moment; lore explanations must not promise era-wide immunity.

### The separate 4A wound

Level 0 rescues the hero from the attempted extraction but does not complete a sealing of that hero's nexus wound. The later shared-level victory repairs its own authored sabotage/outcome. Level 4A returns to the still-unresolved nexus, where the Unbound are renewing their extraction attempt against the escaped legend. The phrase “second incursion” means a renewed attempt at that unresolved wound, not reopening a completed shared level.

Keep existing character-specific settings in real history: Joan's vanguard field, Leonardo's nexus site and the other authored locations do not move into rift space. They can share an era, and even the broader historical episode, with a shared level, but must have a distinct unresolved wound and objective. A different room alone is not the explanation. The 4A threat concerns the legend/nexus; it cannot undo the shared mission's already-sealed historical outcome.

For heroes whose shared home-era mission occurs after 4A, apply the same rule in reverse: sealing the nexus does not automatically solve the later shared sabotage. Each mission resolves its own remaining wound once. Preserve campaign order, all nine 4A variants, level/scene IDs, checkpoints, kit puzzles, Eraser encounter, dust allocation and Integrity budgets.

### Authoring, state and presentation

Each mission dossier/scene manifest must name its stable repaired-moment or wound ID, historical setting, threatened outcome, local anchor and linked siphons, plus any relationship to the selected hero's nexus. Use explicit mapping from the existing stable level/variant ID; do not guess scope from an era label or geographical proximity. Where a Legacy/shared pairing would currently identify the same wound, resolve the narrative/objective mapping before authoring sign-off without invalidating an already completed mission.

N01's sealing interaction closes the rifts and shuts down the local siphons belonging to that mission's wound. It does not globally disable every machine or outstanding mission with the same era label. Persist the repaired-wound scope with the existing completion/sealed result, or reconstruct it from an immutable versioned level-to-wound mapping. Restore completed results on load; never migrate a completed shared level into an unsealed state merely to enable 4A. Actual schema/mapping migration remains implementation work.

Use a concise explanation on the 4A briefing when an earlier repair could make the return confusing, for example: **“The victory you restored still holds. Your nexus is a separate wound—we pulled you free, but never sealed it.”** This is proposed reusable briefing intent, not nine finalized character scripts. Keep the Acts I–II knowledge boundary: no early Forge/harvest explanation is needed.

Completing 4A seals that nexus mission through the same N01 interaction. It does not end the hero's unmoored condition, remove their kit or return them home permanently. Preserve the single total release and voluntary power surrender at the existing final ending. The global ending can resolve the wider crisis without requiring new playable levels for every other legend.

### Pending verification

Review all nine Legacy/shared-level relationships, including shared-before-4A and shared-after-4A ordering. Verify distinct wound/outcome mappings, no reopened victories, correct linked-siphon shutdown and no automatic completion/rewards in the other mission. Check early dialogue/briefings, level results and the final ending for era-wide immunity claims or an unintended early return/power loss. Verify load/migration preserves completed outcomes. Scene mappings, final dialogue and runtime validation remain pending.

## N03 — Shared recognition, larger Legacy homecoming

User selected Option A on 2026-09-12. When the active campaign hero is the central roster legend of a shared historical level, replace its missing-legend beat with **brief recognition of that hero's presence**. Keep the larger personal confrontation, nexus return and emotional payoff in Level 4A. The Legacy Level does not remove recognition from the shared mission or make its present hero “missing.”

Use the existing short home-era/absence dialogue allocation, not an added scene or branching subplot. A local recognition line can replace the absent-legend report; retain the already-budgeted single hero entrance/exit line without adding another monologue. Keep the combined beat within the existing 1–2-line scope. Example replacement copy for the six authored shared home-era pairings:

| Active hero / shared level | Recognition replacing the absence report |
|---|---|
| Leonardo / Florence | **Apprentice:** “Maestro! We need your help at the workshop.” |
| Joan / Orléans | **Captain:** “Joan! The line needs you at the banner.” |
| Tesla / Chicago | **Engineer:** “Mr. Tesla! Help us bring the lights back.” |
| Cleopatra / Alexandria | **Guard:** “Your Majesty! Your people need you.” |
| Shakespeare / The Globe | **Player:** “Master Shakespeare! The company needs you.” |
| Lincoln / Gettysburg | **Union officer:** “Mr. President! We feared you would never reach us.” |

These lines acknowledge presence and the unresolved mission; they do not instantly repair the sabotage, win the battle or complete the level. Use the current dialogue/chirp system and local speaker role, without adding a duplicate NPC version of the playable hero. Final localization and scene delivery remain production work.

### Branch selection and continuity

Select the branch from the saved campaign hero ID and the level's authored central-legend ID before displaying its dialogue or absence-specific vignette:
- Matching IDs: recognition branch; suppress current claims that this hero is missing, imprisoned elsewhere or cannot be found.
- Different IDs: retain that level's existing missing-legend beat and the active hero's outsider observation.
- **M14 (2026-09-26):** every scripted list of, or reference to, missing legends in any scene (for example Sarah's Level 8 "like Florence. Like Orléans.") is built from non-active heroes only. Level 1's exit scene has an explicit Leonardo recognition variant.
- Shared levels without a central playable-roster legend: retain the existing mission/outsider dialogue; no new home-era pairing is invented.

Every other legend's absence beat still applies. Do not suppress the whole Mystery Thread because the active hero has a personal Legacy Level. The branch must work whether that shared level occurs before or after 4A. Do not condition recognition on Legacy completion, and do not imply the hero has returned home permanently: N02's distinct wound and the existing unmoored/final-return rules remain.

The local scene can still show damaged machinery, a stalled advance or an interrupted performance while the hero is present. Remove only contradictory present-tense absence claims/portrayals; preserve unsolved puzzles, encounters, historical stakes and N01's later sealing requirement. No additional companion, playable duplicate or alternate timeline is created.

Keep the Acts I–II knowledge boundary. Recognition does not explain the Forge, harvest, captive location or deficit before their authored reveals. Current absence dialogue may refer to the legend having been gone before this arrival; it cannot insist the visible active hero is still absent.

Resolve stable dialogue/presentation variant IDs from the hero/level mapping, not localized name strings. F10's seen/skip rules must distinguish variants so viewing an outsider's absence scene in one slot cannot substitute it for another slot's recognition. Loading or skipping uses the correct branch without duplicating gameplay effects or changing campaign progress. This does not require replaying already acknowledged scenes.

### Pending verification

Check all six matching hero/level pairs and each nonmatching roster choice, including shared-before-4A and shared-after-4A order. Verify no active hero is called missing in dialogue, briefings or absence-specific art, while other missing-legend beats remain. Check short line scope, speaker placement, existing hero-line budget, no duplicate actor, localized names/glyphs, skip/reload/cross-slot variants and preserved puzzle/boss/sealing state. Final localization, scene authoring and runtime validation remain pending.

## N04 — Named captive scenes exclude the active hero

User selected Option A on 2026-09-12. Keep named recognition in the Moon reveal and individual homecoming shots in the ending. Every captive reference and image must use the campaign roster minus the saved active hero: eight captive roster legends for the current nine-character cast.

### Opening and shared roster selection

All nine characters remain selectable when creating a campaign slot. This is menu availability, not an in-world gathering or nine successful rescues. Level 0 rescues only the chosen hero; only that hero joins the Warden crew. The other eight remain captive, first shown in person in the Extraction Hall and later released in the ending. No extra hub allies, unlocks, character swapping or early captive-location reveal is added.

Derive one stable captive-roster selection from the saved campaign hero ID and authored roster IDs. Use it for Moon dialogue, Extraction Hall occupants/portraits, captive-linked Borrowed Legacies and release imagery. Preserve the existing boss move-selection rules; membership consistency does not require using every captive's move. Do not compare localized display names or use the most recently previewed menu character. The active hero cannot appear in a filled cradle, captive signature portrait, crowd silhouette or earlier return shot. An unoccupied cradle implied by the villain's deficit is not an extra captive.

### Two named examples and return shots

Use the authored priority **Leonardo, Cleopatra, Tesla**, filter out the active hero, then take the first two distinct entries. Leonardo's spoken name here remains “Da Vinci.” Resolve complete localized dialogue variants and shot IDs from these stable identities; placeholders below are authoring notation and must not reach subtitles or voice playback.

| Active campaign hero | Moon names and featured return pair |
|---|---|
| Leonardo | Cleopatra, Tesla |
| Cleopatra | Da Vinci, Tesla |
| Any of the other seven | Da Vinci, Cleopatra |

Sarah's Moon line becomes: “...Coordinates. A fortress in the space between timelines. And inside it — resonance signatures. Alive. It's them. {CaptiveName1}. {CaptiveName2}. The others we lost. They were never adrift. The Unbound have had them since the first strike — and their resonance is draining.”

This retains the existing reveal: location, captivity and draining, without explaining the Forge, deficit or rewrite before Level 14. Keep the scene's current duration/line allocation; no added roll call.

Author three short reusable return shots, of which only two play per campaign:
- **Leonardo:** wakes at his workbench mid-sketch.
- **Cleopatra:** her court finds its queen enthroned.
- **Tesla:** returns to his workshop.

Play the selected pair in the same stable priority order. The subsequent collective return covers the remaining six captive roster legends and the other historical captives; do not stage a second return for the featured pair or include the active hero. Background restoration of an era may appear regardless of the active hero, provided it does not depict that hero already returned.

The active hero remains at the Meridian Founding for the existing farewell and goes home once, last of all the legends. Preserve N01's player-triggered Prime Anchor shattering, total resonance release, mortality, memories and existing ending variant. This changes presentation membership, not rewards, combat, power progression or completion timing.

### Persistence and pending verification

Resolve the selection from the immutable campaign hero identity before presenting dialogue or instantiating scene actors. Stable dialogue/shot/seen/skip variants must distinguish the chosen pair and hero finale, including across save slots. Reuse the same selection after reload; no random reroll or dependence on localized text. Skip/replay must not change N01's already-committed completion or duplicate rewards.

Validate all nine hero choices: exactly eight unique non-active roster captives; two distinct eligible named examples and matching shots; six remaining roster returns; no duplicate active hero anywhere; one final active-hero return. Check opening portraits versus in-world actors, Moon subtitles/voice/portraits, all visible cradle/crowd variants, Borrowed Legacies membership, both ending stills, skip/reload and cross-slot selection. Final scripts/localization, the Tesla substitute shot, scene binding and runtime validation remain pending.

## N05 — Equal-weight ending average

User selected Option A on 2026-09-12. Each timed mission contributes equally to the ending. Keep the 50% threshold on every difficulty and the existing two ending variants; no new reward gate, score system or records screen is added.

### Exact counted set and calculation

The completed campaign contributes exactly **15 unique level IDs**: the fourteen shared levels numbered 2 through 15, inclusive, plus the selected hero's single Level 4A. Exclude untimed Levels 0 and 1, the other eight 4A variants, training and any noncampaign content. Use stable authored level IDs and the saved campaign hero's 4A mapping, including existing legacy scene IDs; do not parse localized titles or take every dictionary entry.

For each counted mission, use its final Integrity locked on successful PreBoss activation, then recorded with its accepted N01 sealing/completion transaction. Boss time, post-boss pickup collection and sealing presentation cannot change that value. The successful completed result is the one contribution; failed attempts, prior restart records and checkpoint samples do not add entries or impose new deductions. F10/F11 recovery rules remain unchanged.

```text
requiredIds = shared Levels 2..15 + selectedHeroLegacyLevelId
require 15 distinct completed IDs with valid recorded final Integrity
total = sum(unrounded levelIntegrity[id] for id in requiredIds)
endingAverage = total / 15
cleanRestoration = total >= 750 percentage points
```

Each valid final percentage is finite and within 0–100. Retain authoritative stored precision; do not round each level or the average before selection. Compare equivalent scaled units if the implementation uses fixed point. Exactly 50% selects the clean restoration; a value below 50% selects the scarred ending even if UI rounding would display 50. This is a rule for the existing ending, not a new numerical meter.

Length, authored par, difficulty multiplier, optional dust, roster identity and narrative era size do not weight the entries. A ten-percentage-point improvement in any one counted level raises the average by 10/15 percentage points. All 4A variants have one equal contribution despite differing layouts and pars.

### Narrative meaning

The average is an abstract measure of the campaign's overall success resisting extraction. Acts I–II preserve historical moments; Act III represents resistance to smothering and disruption of the Unbound's delivery system. Equal contributions express each mission's importance, not equal quantities of harvested energy.

Retain the Forge's fictional use of captive resonance and stolen years, the escaped hero's deficit and both existing outcomes. At 50% or above, the campaign has sufficiently starved/disrupted the operation for the Prime Anchor to fail cleanly. Below 50%, it partially seats before destruction and leaves the existing visible scar. Do not equate the average, its complement or a single Integrity point with a measured quantity of Forge charge. The Act III candle rule still gives the hero full power until smothered; it is not gradual fuel loss.

N01's final shattering, N04's filtered captive release and final hero farewell, retained memories and end-only power loss stay the same. The ending remains a presentation variant, with no mechanical reward locked behind the threshold.

### Persistence and validation

At accepted Level 15 sealing, include its locked final Integrity and completion marker in the same atomic transaction as the final selected ending variant and its rule version. Calculate against that proposed complete 15-level record, not a precommit 14-level dictionary. Commit before the ending presentation. Skip, reload and credits resume the committed variant without recomputation under later tuning or duplicate rewards.

A missing or invalid required record is a save/migration validation problem, not an automatic zero, 100, smaller denominator or guessed level score. Recover trustworthy records under versioned migration/backup rules; preserve the original slot and balances when data cannot be reconstructed and report the unresolved data. Do not invent a new restart penalty or silently change an already-committed ending. Preserve an existing completed save's recorded ending under its recorded rule version; implementation must inspect shipped formats before choosing concrete schema fields/version numbers.

Check all nine hero-to-4A mappings, identical contributions across differing level lengths, exclusion of 0/1 and stray/other-hero entries, exact/just-below/just-above threshold, retained fractional precision, F10/F11 retry histories, the Level 15 transaction, duplicate sealing, interrupted writes, skip/reload and legacy saves. Illustrative arithmetic: fifteen 50% results total 750 and select clean restoration; fourteen 50% results plus one 49.9% total 749.9 and select the scarred ending. These are design examples, not evidence of runtime tests or attainable route balance.

N01–N05 are resolved in design. Scene authoring, save binding/migration, route balance and runtime validation remain pending.
