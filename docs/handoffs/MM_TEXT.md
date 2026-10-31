# MM-TEXT handoff (meter and Defy update, Phase B1)

Branch `mm/text`, base `9cba2b9a`. Not played by anyone: every claim is headless.

## API provided
- `FirstUseTooltips.DefyButton` = `"defy_button"` (key `tooltip_first_use_defy_button`), replacing `FirstUseTooltips.Defy` / `tooltip_first_use_defy` (deleted; its only reader was the presenter, in this workstream's files). `All` still has 9 entries.
- `FirstUseTooltips.DefyButtonDue(float meter)`: meter >= `BasicComboRules.DefyInfluenceCost`. `FirstUseTooltipPresenter._PhysicsProcess` offers it (6-tick poll, skipped-calibration slots only); the `OnDefySealChanged` listener is gone.
- `MoveListScreen.SpecialStatsLine(AbilitySlot, AbilityData)` (internal): Specials use `movelist_ability_momentum` (damage, `MomentumSpecialCostUnits / MomentumUnitsPerPip`, `SpecialReuseDelayFrames / 60`); movement and Ultimate keep `movelist_ability_stats`. A `Defy` row (both modes, never dormant) = `movelist_defy_row` of `movelist_defy_title` and `movelist_defy_body` ({0} = `DefyInfluenceCost`).
- en.csv: new `movelist_ability_momentum`, `movelist_defy_row`, `movelist_defy_title`, `movelist_defy_body`, `tooltip_first_use_defy_button`; rewritten `systems_card_defy_body`, `systems_card_momentum_body`, `dlg_l00_station_defy` (Sarah's line), the Joan/Mozart/Lincoln minor cooldown node names and descriptions (now "+10% Momentum regeneration"), `status_suppression_tooltip` (drops the cooldown clause), `ability_mozart_requiem_chord_description` (drops the Fortissimo clause).

## Decisions
- The Defy tooltip names the button by the word "Defy", not a binding label: `gameplay_defy` does not exist until MM-INPUT merges (merge order puts INPUT first; a follow-up may add a `{0}` argument).
- Old `"defy"` id left in saves' `ShownFirstUseTooltips` is dead data; `KeyFor("defy")` is empty.
- No Siphon Snare row states a Defy-disarm clause, so none was changed.
- `tutorial_step_defy*` keys untouched (MM-SDEFY).

## Deviations / cross-edits
None. Only owned files plus en.csv rows named in the plan.

## Tests (+3, matches plan)
- `FirstUseTooltipsTests.TheDefyButtonTooltipFiresAtTheFirstFiftyAndTheOldSealIdIsRetired`
- `MoveListScreenTests.SpecialsShowTheMomentumLineAndBothModesShowADefyRow`
- `SystemsCardContentTests.TheMomentumAndDefyBodiesDescribeTheGaugeAndTheButton`
Each fails on the pre-change tree (new members do not exist; old csv text contained "once per match" and "take time off"). Not mutation-run beyond that.

Command: `dotnet test --settings .runsettings --filter "FirstUseTooltips|MoveListScreen|SystemsCardContent|CampaignLocalization|UnusedTranslationKey|ScriptTranslationKey|SceneVisibleText|TranslationCsvShape|NarrativeKnowledgeBoundary"` after a local `--import`: Failed 0, Passed 36, Total 36.

## For the docs
Sarah's Level 0 Defy line (design master :4324) now reads: "When they pin you down, Defy. Half a bar breaks history's grip on you, and buys back what they took." Ledger `PROVISIONAL-DEFY-TEXT`, `USER-DEFY-LEVEL0-LESSON`.
