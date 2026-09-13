# Workstream A — Narrative (§ status header, §1 Game Overview, §2 Narrative & Worldbuilding)

Gap dossier. Master: `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md` lines 1–225.
Repo mirror `design-godot.md` is stale at V7.4 (header line 3 says "Mirror synced 2026-08-29 (V7.4)").
Supporting contracts read: `docs/NARRATIVE_RESOLUTION.md` (N01–N05), `docs/PRODUCTION_SCOPE.md` (P01–P04).

Repo root for all paths: `D:\Projects\Fighters Through Time - V3`.

**Standing constraint from the design (do not violate):** *"Legacy scene IDs are retained in code."*
So `level_14_neo_earth`, `level_15_alexandria`, `CampaignLevel.NeoEarth`, `CampaignLevel.Alexandria`,
`Level_14_NeoEarth.tscn`, `neo_earth_*` / `alexandria_*` translation **key names**, boss IDs
`archive_prime` / `apex_eraser`, `chrono_slasher`, and the `boss.apex_eraser.*` / `boss.archive_prime.*`
presentation-event IDs all **stay**. Only the *values* (English copy, display names) and the fiction change.
That one ruling removes most of the churn risk — it keeps `ScenePoolConfigTests`, `StoryAudioSetContentTests`,
`RosterVfxMappingTests`, `LoadingScreenTests`, sprite-atlas paths and the manifest untouched.

---

## 0. Executive summary of the gap

| # | Design change | Status | Size |
|---|---|---|---|
| A1 | Faction/terminology rename (Apex Archive → the Unbound; Chrono-Resistance → Wardens; hijacked Archive Time-Ship → Warden Time-Ship; add the Meridian) | **missing** | M |
| A2 | Origin story rewrite: Library-of-Alexandria overload → the **First Strike**; Resonance is time's immune response, not a side effect of displacement | **missing** | M |
| A3 | **No mid-campaign power loss** — the retired "resonance fades as history heals" arc must be replaced | **missing** (and *pervasively* implemented the old way: ~40 lines across 13 levels) | **L** |
| A4 | Boss renames: The Archive Prime → **The Forge Sentinel**; The Apex Eraser → **The First Unbound**; its P3 → **Borrowed Legacies** | **partial** (rename S; Borrowed Legacies P3 **missing**, M) | M |
| A5 | Level 14 **The Unbound Bastion** (Extraction Cradles / Anchor Forge), Level 15 **The Meridian Founding** (reached by riding the firing channel) | **missing** — repo authors "Neo-Earth core laboratory" and "Library of Alexandria — Restoration" | **L** |
| A6 | Villain's true plan + Act III knowledge boundary (nothing before L13 may state it) | **partial** — L12 pre-boss dialogue already leaks a *different*, now-wrong, endgame | M |
| A7 | Mystery Thread absence beats on 6 level cards + N03 hero-recognition branch | **missing**; no conditional-dialogue plumbing exists at all | **L** |
| A8 | N04 captive-roster selection (roster minus active hero, stable priority Leonardo/Cleopatra/Tesla) | **missing** | M |
| A9 | "Roster must never be hardcoded / enumerated in load-bearing ways" | **missing** — 9 enumeration sites + a hard `FighterCharacterID` enum + 6 tests pinning `9` | M |
| A10 | Two-colour visual grammar (cold Unbound light vs warm gold resonance) | **partial** — `UIPalette.Cyan`/`Gold` exist and are used ad hoc; no contract, no Level 0 ignition beat, no Extraction Hall | M |
| A11 | §1 Core Modes: Steam Remote Play Together is the launch netplay message | **missing** — zero occurrences of "Remote Play" anywhere in the repo | S |
| A12 | §1 Stage Design examples re-pointed to authored stages (Nassau / Vesuvius / Globe, not Titanic / Apollo) | **design-only, no code impact** | — |
| A13 | §1 Gameplay Loop "boss fights against Unbound forces" | covered by A1 | — |
| A14 | `design-godot.md` repo mirror is stale (V7.4) | **missing** — re-copy the master | S |

**Shared files every other workstream will also touch:** `localization/en.csv` (by far the biggest
contention point — this workstream rewrites ~120 rows), `localization/en.en.translation` (must be
regenerated with `--headless --import` and committed), `AGENTS.md`, `design-godot.md`,
`resources/Content/content_manifest.csv` (A4/A8 only if new rows), `scripts/Core/StoryManager.cs`
(A6 level-flow text only), `scripts/UI/DialogueManager.cs` + `DialogueSequenceData.cs` (A7/A8 —
schema change, collides with the §3 hold-to-skip owner), `scripts/Core/SaveManager.cs` (A8 —
`ViewedDialogueIDs` variant IDs), `scripts/UI/UIPalette.cs` (A10, collides with the UI workstream).

---

## 1. A1 — Faction & terminology rename

### What the design now requires
- **The Meridian** = the future's governing order (new proper noun, currently absent from the repo).
- **The Unbound** (working name) replaces *Apex Archive* / *the cult* / *the Archive*.
- **The Wardens** replace *Chrono-Resistance*; the hub is the **Warden Time-Ship**, *their own vessel cut off
  from the future* — explicitly **no longer hijacked**.
- Mobs are "Unbound shock-troopers"; bosses "Unbound Overseers" (Overseer is retained).
- Elites are **Erasers** (the fiction now has a mechanical unit — V7.6 §6, another workstream, but §2's
  Enemy Faction bullet is the authority text).

### What the repo currently does
**Naming collision, already resolved by the master — do not rename:** the existing enemy elite
`chrono_warden` / `enemy_chrono_warden_name,Chrono-Warden` (`resources/Enemies/chrono_warden.tres`,
manifest row 115) is an *enemy*. Master line 2948 rules: *"the enemy's name predates V7.5's 'Wardens'
faction naming and is retained — in fiction, a stolen title worn mockingly."* Action: add that lore note
as a comment in the resource/level dossiers; **no rename, no test change**.
`ability_tesla_wardenclyffe_cataclysm_*` is Wardenclyffe Tower — unrelated, leave alone.

#### `localization/en.csv` — keys whose English value must change (A1 only; A2/A3 add more)

| Line | Key | Current value | Note |
|---|---|---|---|
| 145 | `boss_archive_prime_name` | `Archive Prime` | → see A4 |
| 146 | `boss_apex_eraser_name` | `The Apex Eraser` | → see A4 |
| 237 | `boss_ability_archive_purge_name` | `Archive Purge` | ability display name, faction word |
| 243 | `boss_ability_archive_remnants_name` | `Archive Remnants` | ability display name, faction word |
| 522 | `hub_ship_title` | `THE ARCHIVE TIME-SHIP` | → `THE WARDEN TIME-SHIP` |
| 523 | `hub_interaction_help` | `Archive Time-Ship \| [E] Interact with Portal / Repository / Holodeck / Crew` | |
| 636 | `campaign_level_neo_earth` | `Neo-Earth — The Apex Archive` | → A5 |
| 637 | `campaign_level_alexandria` | `Library of Alexandria — Restoration` | → A5 |
| 661 | `orleans_objective_towers` | `Sabotage the Apex shield generators ({0}/{1})` | |
| 871 | `gettysburg_objective_arrays` | `Destroy the Apex shielding arrays ({0}/{1})` | |
| 892 | `lunar_objective_reach_outpost` | `Cross the mare and reach the Archive outpost` | |
| 949 | `neo_earth_level_title` | `Neo-Earth — The Apex Archive` | → A5 |
| 958 | `neo_earth_objective_defeat_boss` | `Shut down Archive Prime` | → A4 |
| 963 | `speaker_archive_prime` | `Archive Prime` | → A4 |
| 974 | `alexandria_level_title` | `LEVEL 15: THE LIBRARY OF ALEXANDRIA — RESTORATION` | → A5 |
| 983 | `alexandria_objective_defeat_boss` | `Defeat the Apex Eraser!` | → A4 |
| 989 | `speaker_apex_eraser` | `The Apex Eraser` | → A4 |

Dialogue lines containing `Archive` / `the cult` / `cultists` / `Chrono-Resistance` (value rewrite;
**all of these also need A2/A3 content edits**, so treat them as one rewrite pass):

`dlg_l00_intro_1` (Chrono-Resistance), `dlg_l00_intro_3` (Archive Time-Ship + "Apex Archive" + "futuristic
cult"), `dlg_l00_intro_5` ("the cult's doing"/"cultists"), `dlg_l01_entrance_2` ("The cult has deployed"),
`dlg_l01_boss_1` ("The Archive has promised"), `dlg_l02_entrance_2`, `dlg_l02_boss_1`,
`dlg_l03_entrance_2`, `dlg_l04_entrance_2` ("The cult wants"), `dlg_l04_boss_1` ("sedition against the
Archive"), `dlg_l05_entrance_2`, `dlg_l05_exit_4`, `dlg_l06_entrance_2`, `dlg_l07_entrance_2`,
`dlg_l08_entrance_2`, `dlg_l08_postboss_2` ("The cult's mind control"), `dlg_l10_entrance_2`,
`dlg_l11_entrance_2`, `dlg_l11_exit_2`, `dlg_l12_entrance_2`, `dlg_l12_preboss_1` ("the cult has built"),
`dlg_l12_exit_2`, `dlg_l12_exit_4`, `dlg_l13_entrance_2`, `dlg_l13_exit_5`, `dlg_l14_entrance_2`,
`dlg_l14_exit_4`, `dlg_l15_preboss_3` ("three phases of Archive doctrine").

#### Code comments / identifiers (non-string, cosmetic but should follow)
`scripts/Environment/ChronalExtractor.cs:198` — `/// <summary>Apex Archive cyan...` (and the constant
`DischargeColor` — see A10). `Level02Controller.cs:12,135`, `Level03Controller.cs:81`,
`Level05Controller.cs:12,35`, `Level06Controller.cs:33,131`, `Level07Controller.cs:12,44`,
`Level08Controller.cs:134,193`, `Level09Controller.cs:83,98`, `Level10Controller.cs:11,173`,
`Level11Controller.cs:185,265`, `Level12Controller.cs:13,193,211`, `Level13Controller.cs:14,254,276`,
`Level14Controller.cs:39,63,69,94,260,281,283`, `Level15Controller.cs` (18 hits),
`scripts/UI/RewindPresentationOverlay.cs` (2 hits). `CultistEnemyID` constants
(`Level07/09/12/13Controller`) point at `chrono_slasher` — the *ID* stays; only the C# const name and
XML doc read "Cultist". Low value; rename only if cheap.

### Status / size
**missing**. Size **M** (mechanical, but ~120 CSV rows and ~50 comment sites). Must end with
`--headless --import` and a committed `localization/en.en.translation`, or every touched key renders
raw at runtime (`CampaignLocalizationTests` will catch it).

---

## 2. A2 — Origin story: the First Strike, and Resonance as time's immune response

### What the design now requires
- The inciting incident is the **First Strike**: a coordinated blitz aimed at *people* (history's legends),
  not at the Library of Alexandria. Extraction beams **crack the target's nexus moment open**.
- **Temporal Resonance is time fighting back** — history arming its legend as the beam cracks the nexus.
  Not the Unbound's tool (their cradles smother it), not the Wardens' gift (their held beam only bought
  the seconds). Explicitly **not** "a side effect of your displacement".
- The Wardens reached exactly **one** strike (the player's); every other legend was taken clean and is
  now cradled in the Unbound Bastion.
- The Warden Time-Ship was **cut off** in the same instant (not hijacked).
- The **Time Rule**: one mutable timeline, unmooring, cracking a moment (sabotage *is* the extraction
  method), and N01 sealing.

### What the repo currently does
`dlg_l00_intro_3` is the canonical statement of the retired fiction, verbatim:

> "You are aboard the Archive Time-Ship. The people who did this to your home call themselves the Apex
> Archive. They are a futuristic cult siphoning the historical energy of pivotal eras to build their
> 'perfect' timeline. **Your displacement was a side effect — but it also imbued you with Temporal
> Resonance**, the power to fight back."

`dlg_l00_intro_2` "...Where am I? The sky... it tore apart." survives the rework (the beam *does* tear the
sky) — keep. `dlg_l15_entrance_2` states the Library overload is "the fracture every rift you have closed
was torn out of" — retired outright.

Level 0's Part 1 is a code-built "fracture presentation":
`scripts/Environment/Level00Controller.cs` — `TutorialPhase.Fracture`, `StartFracturePresentation()`
(line 93), `BuildFracturePresentation()` (line 525: a purple `ChronalFracture` Node2D at (250,700),
`tutorial_fracture_label` = "CHRONAL FRACTURE"), `tutorial_objective_fracture` = "Listen to Commander
Sarah's briefing". The design's *"rebuilt beat sequence"* establishing the two-colour grammar playably
(A10) has no counterpart here.

Dialogue resource: `resources/Dialogue/level_00_dialogue.tres`, sequence `level_00.intro` has exactly
**6 lines** (`dlg_l00_intro_1..6`, speakers sarah/player/sarah/player/sarah/sarah). A First-Strike
re-write likely needs a different line count — that's a `PackedStringArray` edit in three parallel
arrays (`SpeakerNameKeys`, `LineKeys`, `EmotionKeys`), all pinned length-equal by
`CampaignLocalizationTests` (speakers == lines) and `Level00ContentTests`.

### Concrete change list
1. Rewrite `dlg_l00_intro_1..6` (extend to 7–9 if the First Strike + Time Rule needs it); update
   `level_00_dialogue.tres` `Intro` sub-resource arrays in lockstep.
2. Rewrite `dlg_l15_entrance_1`, `dlg_l15_entrance_2` (no "original overload"; the destination is the
   Meridian Founding, reached by riding the Forge's firing channel).
3. `dlg_hub_sarah_1/2` — reframe as Warden crew, cut-off ship.
4. Optional new keys if the Time Rule needs stating: `dlg_l00_intro_7..9`, `speaker_sarah` unchanged.
5. Update `AGENTS.md` "Narrative and mode context" (currently states the Apex Archive / Library of
   Alexandria overload / hijacked Time-Ship canon verbatim, ~4 paragraphs).

### Status / size
**missing**. **M** on its own; folds naturally into the A3 rewrite pass.

---

## 3. A3 — "No mid-campaign power loss" (the biggest single content gap)

### What the design now requires
> "The power is history's own, held in trust — and it **grows** across the campaign … There is **no
> mid-campaign power loss**; the hero surrenders the charge exactly once, by choice, at the very end."

Every scrap of resonance reclaimed (Chronal Dust, shattered Extractors, sealed moments) *feeds* the hero.
The only power loss in the game is the single total release at the Meridian Founding.

### What the repo currently does
The retired arc is not a stray line — it is a deliberate, **per-level three-beat structure** authored into
the exit dialogue of essentially every level, plus two boss intros and the ending. Full inventory:

| Key | Current English (verbatim, abridged where long) |
|---|---|
| `dlg_l01_exit_3` | "I can feel it. My hands... they are glowing less. The stabilization of the area is dampening my own temporal resonance." |
| `dlg_l01_exit_4` | "Yes, that is the cost. Your powers are anchored to the temporal fractures. **The more we repair history, the more your resonance will fade.** But it is the only way to save your world." |
| `dlg_l01_exit_5` | "A small price to pay. Let's return to the ship." |
| `dlg_l02_exit_3` | "The glow in my hands dimmed again when the field collapsed. It happens every time we mend something." |
| `dlg_l02_exit_4` | "It does. Your resonance is borrowed from the damage, and we are spending it repairing the damage..." |
| `dlg_l02_exit_5` | "Then let's spend it faster. Take me back to the ship." |
| `dlg_l03_exit_3` | "The resonance is fading already. I can barely feel the grid now." |
| `dlg_l03_exit_4` | "It always fades once the moment is anchored. That is how we know it held." |
| `dlg_l03_exit_5` | "Then anchor the next one. Bring me home, Commander." |
| `dlg_l04_exit_3` | "The glow in my hands dimmed again. Every fracture I close takes a little more of it." |
| `dlg_l04_exit_4` | "It does. Your resonance is borrowed from the damage, and we are giving the damage back..." |
| `dlg_l04_exit_5` | "It is the only trade on offer. Bring me home, Commander." |
| `dlg_l06_exit_3` | "It went dim again. Further than last time. Halfway up the ash road I felt it go out completely, for a second." |
| `dlg_l06_exit_4` | "...Your resonance is drawn out of the fracture, and every fracture you close is a little less of you..." |
| `dlg_l06_exit_5` | "Log it, and bring me home. There are nine more of these." |
| `dlg_l07_exit_3` | "The resonance is thinning again. I could feel every rope before I touched it, and now I can barely feel my own hands." |
| `dlg_l07_exit_4` | "It always fades once the fracture closes. Come home and let the ship read you..." |
| `dlg_l08_postboss_3` | "...Yet... the power I held. The sand obeyed my command... Now, it is just dust again." |
| `dlg_l08_postboss_4` | "**Our resonance is a symptom of a sick timeline. As the wound heals, the strength fades.** We must see this through to the end." |
| `dlg_l09_exit_3` | "It is going faint again. The resonance. Every fracture we close, I hold a little less of it." |
| `dlg_l09_exit_4` | "I know. You are giving it back to the moments it was stolen from..." |
| `dlg_l09_exit_5` | "Then stop trying to and get me to the next one. Bring me home, Commander." |
| `dlg_l10_exit_3` | "It went faint again. Halfway across the rigging I reached for the resonance and there was a beat where nothing came back." |
| `dlg_l10_exit_4` | "...Every fracture you seal, you hand a little more of it back to the moment it was stolen from..." |
| `dlg_l10_exit_5` | "Then log it and line up the next one. Bring me home, Commander." |
| `dlg_l11_exit_3` | "Sarah. Climbing that last bank I reached for the resonance and for half a second there was nothing there. Just me." |
| `dlg_l11_exit_4` | "...The gap is longer every level... every fracture you close is drawn out of you..." |
| `dlg_l11_exit_5` | "Log it. Then pull me out. There are four more of these." |
| `dlg_l12_preboss_2` | "...once the prime anchor at Alexandria is restored in the final act, the timeline will snap back to normal." |
| `dlg_l12_preboss_3` | "And what happens to us then?" |
| `dlg_l12_preboss_4` | "...**you will lose all of your powers, since the changes were centred on your displacement.** You will be mortal again." |
| `dlg_l12_preboss_5` | "Then let us make this final legacy count." |
| `dlg_l12_exit_3` | "It went out completely on the pad, Sarah. The resonance. For a second I was just a person standing on the Moon..." |
| `dlg_l13_boss_intro_1` | "**You have shed a piece of yourself in every century you closed.** Did you think they went nowhere? I am every second of resonance the fractures took out of you..." |
| `dlg_l13_boss_intro_2` | "Then you know exactly how I fight, and exactly how tired I am. So do I. Come on." |
| `dlg_l13_exit_3` | "Sarah, when it went out I reached for the resonance and for a second I could not tell whose it was..." |
| `dlg_l13_exit_4` | "...That was the price, and it is closer now than it was on the Moon..." |
| `dlg_l14_exit_3` | "It faded again, Sarah. Halfway up the containment shaft the resonance thinned right out... It comes back slower every time now." |
| `dlg_l14_exit_4` | "I know, and I will not pretend that is not the clock..." |
| `dlg_l15_ending_3` | "The temporal resonance... it is fading. The weight of the era is returning." (**this one survives** — it is the single end-of-game release; keep, re-word for the total release) |
| `dlg_l15_ending_4` | "...but the magic of the rifts is gone. You are back where you belong." (survives; re-word) |

Note `dlg_l05_exit_*` (Titanic) and `dlg_l15_*` are the exceptions — L5 uses its slot for the uplink-handshake
plot hook instead. Level 8 uses `postboss` rather than `exit`.

**`dlg_l13_boss_intro_1` is structurally load-bearing:** the Mirror Paradox's entire characterisation is
"I am the resonance you shed." With no mid-campaign power loss, the Mirror Paradox needs a new premise.
The encounter code (`MirrorParadoxController`, `MirrorParadoxEncounterController`) is unaffected — only
the two intro lines and `dlg_l13_exit_1/3/4`.

### Concrete change list
- Rewrite ~40 `dlg_l*_exit_3/4/5` + `dlg_l08_postboss_3/4` + `dlg_l12_preboss_2..5` + `dlg_l12_exit_3` +
  `dlg_l13_boss_intro_1/2` + `dlg_l13_exit_3/4` + `dlg_l14_exit_3/4` values in `en.csv`.
  The design gives the replacement theme: the power **grows**; the beat becomes "what you reclaimed this
  era is now yours" and (Acts I–II) the Wardens' honest, mistaken read of the enemy plan.
- Line **counts** stay the same wherever possible so the `.tres` sequences in
  `resources/Dialogue/level_0X_dialogue.tres` need no array edits. Recommend holding count constant —
  each level's content test pins `sequence.LineKeys.Length == SpeakerNameKeys.Length` and the per-level
  suites (`Level02ContentTests` … `Level15ContentTests`) enumerate the exact key lists.
- `AGENTS.md`: nothing currently asserts the fade arc, but add the no-power-loss rule to
  "Product identity and non-negotiable pillars" so future agents cannot re-introduce it.

### Status / size
**missing** — and the repo actively implements the retired design. Size **L** (it is a writing task across
13 levels, not a code task). Zero mechanical/code risk; pure `en.csv` value churn + reimport.

---

## 4. A4 — Boss renames and Borrowed Legacies (P3)

### What the design now requires
- **The Archive Prime → The Forge Sentinel** (Level 14).
- **The Apex Eraser → The First Unbound** (Level 15); its **Phase 3 becomes "Borrowed Legacies"** —
  it channels the **captive roster's signature moves**, *data-driven from the roster minus the active
  character* (explicitly roster-scaling; see A8/A9).

### What the repo currently does
- `resources/Bosses/archive_prime.tres` — `BossID = "archive_prime"` (keep), `DisplayName = "Archive Prime"`,
  `DisplayNameKey = "boss_archive_prime_name"` (keep the key, change the value), 1050 HP, 3 phases.
- `resources/Bosses/apex_eraser.tres` — `BossID = "apex_eraser"` (keep), `DisplayName = "The Apex Eraser"`,
  `DisplayNameKey = "boss_apex_eraser_name"`, 1200 HP, `PhaseThresholds = [0.66, 0.33]`,
  5 abilities with `AbilityMinPhase = [0,0,1,0,2]`. **The only phase-2-gated ability is
  `archive_remnants`** (`resources/Bosses/Abilities/apex_eraser/archive_remnants.tres`) — a
  `SummonMinions` archetype that summons `chrono_slasher`. That is the current P3 identity and it is
  *not* Borrowed Legacies.
- Content manifest rows 131/132 carry the IDs only — **no display-name column**, so no manifest edit
  needed for the rename.
- Sprite atlases: `assets/sprites/bosses/apex_eraser/retro/*`, `archive_prime/retro/*`;
  `resources/SpriteFrames/Bosses/{apex_eraser,archive_prime}_{frames,ability_vfx_frames}.tres` keyed by
  `boss.<id>.<ability>` animation names. All keep their IDs.
- `scripts/Combat/EnemyAbilityVisualLibrary.cs:29` lists `"apex_eraser", "archive_prime"` — IDs, keep.

### Concrete change list
**Rename (S):**
1. `en.csv` 145 `boss_archive_prime_name` → `The Forge Sentinel`; 963 `speaker_archive_prime` → same;
   958 `neo_earth_objective_defeat_boss` → "Shut down the Forge Sentinel".
2. `en.csv` 146 `boss_apex_eraser_name` → `The First Unbound`; 989 `speaker_apex_eraser` → same;
   983 `alexandria_objective_defeat_boss` → "Defeat the First Unbound!".
3. `en.csv` 237 `boss_ability_archive_purge_name`, 243 `boss_ability_archive_remnants_name` — re-word
   ("Forge Purge", "Borrowed Legacies") while leaving the keys and `AbilityID`s alone.
4. `DisplayName` field in both `.tres` (a non-localized debug field; keep in sync).
5. `tests/unit/StoryHudPresentationTests.cs:83` uses `"boss_archive_prime_name"` as a key — unaffected
   (key unchanged).

**Borrowed Legacies P3 (M, cross-workstream):** replace/augment `archive_remnants.tres` with an ability
whose executed moveset is *derived at runtime from the roster minus the active hero*. Today there is no
data path for that: `EnemyAbilityExecutor` archetypes are fixed (`SummonMinions, MeleeStrike, Projectile,
Shockwave, ChargeDash, ShieldBubble, AreaPulse, Teleport, PersistentFieldAtTarget`). This wants either a
new archetype or a `BossController` hook that reads `CharacterData`/`AbilityData` for N roster members.
Touches `scripts/Enemies/EnemyAbilityExecutor.cs`, `BossController.cs`, `resources/Bosses/Abilities/
apex_eraser/`, and needs `AbilityMinPhase` to stay `[…,2]`. **Flag to the boss/roster workstream** —
it is the mechanical half of A8/A9's "roster is data".

### Status / size
Rename **partial→trivial (S)**. Borrowed Legacies **missing (M)**.

---

## 5. A5 — Level 14 "The Unbound Bastion" and Level 15 "The Meridian Founding"

### What the design now requires
- **L14 = the Unbound Bastion**, a fortress in the space between timelines, containing the **Extraction
  Cradles** (the captive roster, plus rows of lesser figures receding into the dark) and the **Anchor
  Forge**. Its *Extraction Hall* is a named beat showing thin gold threads drawn off cold from each cradle.
  The player destroys the Forge's **intake** but **cannot free the captives** (severing a charged cradle
  consumes the captive; only killing the machine releases them). N01: the L14 interaction triggers the
  authored firing/escape-to-L15 sequence.
- **L15 = the Meridian Founding**, reached by **riding the Forge's own firing channel** (the channel exists
  only while it fires — which is why the fight happens *during* the firing). The First Unbound tries to
  seat the **Prime Anchor** by force. Victory shatters the half-seated Prime Anchor → total release.
- Legacy scene IDs retained: `Level_14_NeoEarth.tscn`, `level_14_neo_earth`, `Level_15_Alexandria.tscn`,
  `level_15_alexandria`, `neo_earth_*`/`alexandria_*` keys.

### What the repo currently does
`scripts/Environment/Level14Controller.cs` authors **"Neo-Earth, the Apex Archive core laboratory"** —
rooms: Breach Gallery, Containment Wing, Fabrication Spine, Security Core; objectives:
`neo_earth_objective_breach/containment/core_approach/security_core/defeat_boss/complete`; hazards:
security lattices + anti-grav pockets (`neo_earth_pocket_alpha/beta/gamma`). Comment line 63: *"The arena
is the boss. Archive Prime is the security core itself."* `dlg_l14_entrance_1` explicitly says *"not one
person in it who was ever taken"* and `dlg_l14_exit_2` *"there is nobody down there to bring home… no
locals, no coerced crews, nothing to restore"* — **directly contradicts** the Extraction Cradles canon.

`scripts/Environment/Level15Controller.cs` authors the **burning Library of Alexandria**: rooms
`alexandria_room_portico/stacks/collapse/rotunda` ("THE BURNING PORTICO", "THE GREAT STACKS", "THE
COLLAPSING HALL", "THE PRIME ANCHOR ROTUNDA"); objectives `alexandria_objective_reach_anchor/stacks/
escape/rotunda/restore/complete`; `alexandria_objective_restore` = "Insert the Temporal Core into the
Alexandria Anchor"; `alexandria_anchor_dormant/ready` = "PRIME ANCHOR — SEALED/EXPOSED";
`alexandria_interaction_insert_core`. `dlg_l15_entrance_1/2` place the level inside the original overload.

Note the **Prime Anchor polarity has inverted**: in the repo it is history's restoration stone the hero
fills with the Temporal Core; in V7.5 it is the *Unbound's weapon* aimed at rewriting the Meridian
Founding, which the hero shatters. Same words, opposite meaning — a rewriter must not assume the existing
copy is salvageable.

### Concrete change list
1. `en.csv` value rewrites, keys unchanged: `neo_earth_level_title`, `neo_earth_room_*` (4),
   `neo_earth_objective_*` (6), `neo_earth_pocket_*` (3), `campaign_level_neo_earth`;
   `alexandria_level_title`, `alexandria_room_*` (4), `alexandria_objective_*` (6),
   `alexandria_anchor_dormant/ready`, `alexandria_interaction_insert_core`, `campaign_level_alexandria`.
2. `en.csv` dialogue rewrites: `dlg_l14_entrance_1..3`, `dlg_l14_boss_intro_1/2`, `dlg_l14_exit_1..5`;
   `dlg_l15_entrance_1..3`, `dlg_l15_preboss_1..3`, `dlg_l15_boss_intro_1/2`, `dlg_l15_postboss_1/2`,
   `dlg_l15_ending_1..6`.
3. New keys likely needed: an Extraction Hall room/beat (`neo_earth_room_extraction_hall`), the
   cradle-sever refusal line, the firing-channel ride (`alexandria_room_firing_channel`), the N01
   "Seal Timeline — Complete Level" prompt (cross-workstream, §3).
4. Scene/level content (L, cross-workstream with §3/§16): the Extraction Hall room and its named cradle
   actors (A8 selection), the L15 firing-channel route, the Prime Anchor shatter interaction replacing
   `alexandria_objective_restore`'s Temporal Core insertion.
5. Tests to update: `tests/ContentValidation/Level14ContentTests.cs` (lines 149–155 enumerate every
   `neo_earth_*` key), `Level15ContentTests.cs` (lines 164, 183 enumerate `alexandria_*` keys and the
   `alexandria_interaction_insert_core` interaction). These are **key-name** lists, so a values-only
   rewrite leaves them green; adding/removing a room or objective key does not.
6. `scripts/Environment/Level14Controller.cs` / `Level15Controller.cs` XML doc comments (33 hits combined).

### Status / size
**missing**. Copy/keys **M**; the Extraction Hall + firing-channel scene work is **L** and overlaps the
level-authoring workstream. `dlg_l14_exit_2`'s "nobody to bring home" is a *hard contradiction* — call it
out rather than softening it.

---

## 6. A6 — The villain's true plan and the Act III knowledge boundary

### What the design requires
*"No dialogue line, level brief, or UI string may state any of it before Level 13."* Through Acts I–II the
game presents only the Wardens' honest, mistaken read (the sabotage *is* the plan). The where/why reveals
split between **Level 12** (Sarah's Moon line: location + captivity + draining, **not** the Forge/deficit/
rewrite) and **Level 14**.

### What the repo currently does — a leak, of the *old* endgame
`dlg_l12_preboss_2` and `dlg_l12_preboss_4` (Level 12, Act II) state the entire retired endgame in advance:
the prime anchor at Alexandria will be restored, everyone goes home, the hero loses all powers. Under V7.5
this is both wrong fiction (A3) *and* a knowledge-boundary violation shape — L12's slot is now reserved for
N04's Moon reveal ("Coordinates. A fortress in the space between timelines… {CaptiveName1}. {CaptiveName2}…").

`dlg_l12_exit_4` "It is their home. Far future. Neo-Earth." is the current Act-II-to-Act-III hinge and is
structurally the right place for the Moon reveal — repoint it at the Bastion.

Nothing else in Acts I–II leaks the Forge (it did not exist before), so the boundary is otherwise clean.

### Concrete change list
- Rewrite `dlg_l12_preboss_1..5` and `dlg_l12_exit_1..5` to N04's specified Moon line, with the two
  captive names resolved from the A8 selection.
- Add a repo-side guard if cheap: a content test sweeping `dlg_l0*`–`dlg_l12_*` values for the forbidden
  proper nouns (`Anchor Forge`, `Prime Anchor`, `Extraction Cradle`, `Meridian Founding`, `the Landing`,
  `deficit`). Cheap, pure-C#, and it is the only mechanical way to keep a knowledge boundary from eroding.
  Suggested home: a new `tests/ContentValidation/NarrativeKnowledgeBoundaryTests.cs` (**one `[TestSuite]`
  per file** — CLAUDE.md failure signature 6).

### Status / size
**partial**. **M**.

---

## 7. A7 — Mystery Thread absence beats + N03 hero-recognition branch

### What the design requires
Six level cards gain an authored **Absence** line (Florence/Leonardo, Orléans/Joan, Chicago/Tesla,
Alexandria/Cleopatra, The Globe/Shakespeare, Gettysburg/Lincoln). When the active campaign hero **is**
that level's central legend, the absence report is replaced by **brief recognition** (N03 Option A; the
exact six replacement lines are tabled in `NARRATIVE_RESOLUTION.md` §N03). Branch selection is from the
**saved campaign hero ID vs the level's authored central-legend ID** — *never* from localized names.
Variant IDs must be distinct so `F10` seen/skip rules cannot substitute one for the other across slots.

### What the repo currently does
- **No absence beats exist.** The entrance dialogue for each of the six levels is faction briefing only
  (e.g. `dlg_l01_entrance_1/2`, `dlg_l02_entrance_1..3`).
- **No conditional/variant dialogue plumbing exists.** `scripts/UI/DialogueSequenceData.cs` is a flat
  resource: `DialogueID`, `SpeakerNameKeys`, `LineKeys`, `EmotionKeys`, `SpeakerPortraits`,
  `PausesGameplay`, `AutoAdvance`, `AutoAdvanceDelay`. No condition, no variant, no hero gate.
  `DialogueManager.StartSequence(string dialogueID)` does a flat lookup (line 276).
- The only hero-aware hook that exists is the `speaker_player` convention: `DialogueSequenceData`'s doc
  comment says *"the speaker key `speaker_player` is resolved at runtime to the selected campaign
  character's localized name"* (`en.csv:559` `speaker_player,Traveler`). That is the precedent to extend.
- `DialogueManager` already persists `save.ViewedDialogueIDs` / `save.LastViewedDialogueID`
  (lines 577–585) for hold-to-skip — so **variant IDs must be distinct strings**, or viewing the
  outsider variant in one slot marks the recognition variant seen in another. N03 calls this out explicitly.

### Concrete change list
1. Schema: add `CentralLegendID` (string, empty = no pairing) to the *level* side and a variant pair to
   `DialogueSetData`/`DialogueSequenceData` — e.g. `DialogueID = "level_01.entrance"` plus
   `"level_01.entrance.recognition"`, selected by `StoryManager`/`StorySceneBootstrapper` from
   `GameManager.Instance.CurrentSession.SelectedCharacterID`. Keep the resolution in one helper so the
   N04 selection (A8) can reuse it.
2. `en.csv`: 6 × absence line keys + 6 × recognition line keys (12 new rows minimum), e.g.
   `dlg_l01_absence_leonardo` / `dlg_l01_recognition_leonardo`, plus new speaker keys
   (`speaker_apprentice`, `speaker_captain`, `speaker_engineer`, `speaker_guard`, `speaker_player_company`,
   `speaker_union_officer`) — `CampaignLocalizationTests` floors `speaker_*` at 10 (currently well above).
3. `resources/Dialogue/level_0{1,2,3,8,10,11}_dialogue.tres`: add the two sequences per file.
4. Tests: per-level content suites enumerate exact sequence lists (`Level02ContentTests.cs:249` asserts
   `set.Sequences.Length == 3`) — each of the six will need its count bumped and both variants pinned.
   `CampaignLocalizationTests` floors: `dlg_l*` ≥ 160, sequences ≥ 48 — additions only, safe.
5. **The four non-roster levels (Paris, Titanic/L5, Pompeii, Nassau, Berlin, Lunar) get no pairing** —
   N03: "Shared levels without a central playable-roster legend: retain the existing mission dialogue."

### Status / size
**missing**, including the plumbing. **L**. Collides with the §3 hold-to-skip owner in
`DialogueManager.cs` and with `SaveManager` (`ViewedDialogueIDs`).

---

## 8. A8 — N04 captive-roster selection

### What the design requires
Every captive reference/image uses **the campaign roster minus the saved active hero** (8 for today's 9).
One stable selection derived from the saved hero ID + authored roster IDs, reused for: the Moon dialogue
names, Extraction Hall cradle occupants/portraits, **captive-linked Borrowed Legacies** (A4), and the
ending release imagery. Named-example priority is **Leonardo, Cleopatra, Tesla** → filter out the active
hero → take the first two distinct. Leonardo's spoken name is **"Da Vinci"**. Three reusable return shots
(Leonardo/Cleopatra/Tesla), two play per campaign. The active hero may **never** appear in a filled cradle,
captive portrait, crowd silhouette or early return shot.

### What the repo currently does
Nothing. No captive concept, no roster-minus-hero helper, no `{CaptiveName1}` substitution path.
`en.csv` has no `captive_*` family. The `Tr()` pipeline supports `{0}`-style formatting (e.g.
`hud_integrity,Integrity {0}%`), so parameterised captive names are expressible.

The roster-minus-hero derivation must read from a **data** roster, not a hardcoded array — see A9.

### Concrete change list
- New `FTT.Core` helper (suggest `CampaignCaptiveRoster`): `IReadOnlyList<string> For(string heroID)`
  from the manifest's `Character` rows (`ContentManifest.ForCategory(ContentCategory.Character)`) minus
  the hero, plus `(string, string) NamedExamplesFor(string heroID)` implementing the
  Leonardo→Cleopatra→Tesla priority.
- `en.csv`: spoken-name family for captives (`captive_name_leonardo,Da Vinci`, etc. — **distinct from**
  the existing `character_*` display names, because Leonardo's spoken name differs), the parameterised
  Moon line, and three return-shot keys.
- Tests: a pure-C# suite pinning "exactly rosterCount−1 captives, active hero never present, priority
  order stable, all nine hero choices" — and it must be written **count-agnostically** (A9).

### Status / size
**missing**. **M**. Shares `en.csv`, `ContentManifest`, and the A7 variant plumbing.

---

## 9. A9 — "The roster will grow: never hardcode size or enumerate the cast in load-bearing ways"

### What the design requires
> "Standing mandate: the roster will grow — narrative and spec text must never hardcode roster size or
> enumerate the cast in load-bearing ways."

Reinforced by `NARRATIVE_RESOLUTION.md` N04 ("eight captive roster legends **for the current nine-character
cast**"), N05 ("15 unique level IDs … the selected hero's single Level 4A"), and P03 ("makes roster scaling
explicit in the narrative mirror"). And by A4's Borrowed Legacies being "data-driven from the roster minus
the active character".

### What the repo currently does — the load-bearing hardcodes

**Production code, literal 9-element arrays (all identical, all `einstein, joan, leonardo, lincoln,
cleopatra, tesla, shakespeare, mozart, pocahontas`):**

| File:line | Symbol | Load-bearing? |
|---|---|---|
| `scripts/UI/MainMenu.cs:41-45` | `RosterIDs` — comment literally says *"The locked nine-character roster"* | **yes** — drives the character-select grid |
| `scripts/UI/CharacterSelectScreen.cs:63-66` | `_characterIDs` | **yes** — Fighter select tiles |
| `scripts/UI/HolodeckConsolePanel.cs:29-32` | `RosterIDs` | **yes** — CPU opponent list |
| `scripts/Core/SaveManager.cs:117-120` | `GlobalSaveData.UnlockedCharacters` default | **yes** — persisted unlock set |
| `scripts/Combat/AbilityVisualLibrary.cs:14-17` | `RosterIDs` HashSet | yes (VFX resolution gate) |
| `scripts/FighterSim/FighterSimulationComponents.cs:7-17` | `enum FighterCharacterID { Einstein=0 … Pocahontas=8 }` | **yes, and hardest** — the deterministic sim's character identity; `FighterLoadoutFactory.cs:81+` maps strings to it; `FighterEntitySystems.cs` encodes `zoneTypeID = (int)FighterCharacterID.X * 10 + slot` in **~20 places**. Adding a 10th character is a protocol-affecting change. |
| `scripts/UI/MainMenu.cs:314` | `session.SelectedCharacterID = "einstein"` fallback | benign |
| 7 × `?? "einstein"` fallbacks (`StoryLevelControllerBase:344`, `Level00/01Controller`, `HubWorldController:429`, `FighterStageController:23`, `TestArenaController:12`) | benign default |
| `scripts/Characters/CharacterFactory.cs:18,229,264` | per-character colour map / ability switch / hitbox names | inherent (per-character behaviour) — but should key off data, not a `switch` |

**Tests that pin the number 9 (these are the ones that will *block* a roster addition):**
- `tests/ContentValidation/CharacterManifestTests.cs:15,22,23` — `InitialRoster.Length == 9`, `ids.Count == 9`.
- `tests/ContentValidation/CharacterPresentationTests.cs:15,136,137,160` — `== 9` three times.
- `tests/ContentValidation/ContentManifestTests.cs:20,22` — `Character` rows `== 9`, `ResonanceGrid` rows `== 9`.
- Roster arrays duplicated in `AbilityVfxAssignmentTests.cs:26`, `DialogueChirpPitchTests.cs:31`,
  `DustEconomyTests.cs:20`, `Level12ContentTests.cs:38`, `Level13ContentTests.cs:44`,
  `Level14ContentTests.cs:39`, `Level15ContentTests.cs:41`.
- ~20 "the nine …" XML doc comments across `tests/`.

**Data side is already fine:** `resources/Content/content_manifest.csv` rows 32–40 are the authoritative
roster and are queryable via `ContentManifest.ForCategory(ContentCategory.Character)`. That is the seam.

### Concrete change list
1. Introduce one canonical runtime roster source (suggest `FTT.Core.CharacterRoster.IDs` backed by the
   manifest, cached like `AuthoredResources`) and repoint the 5 UI/save/VFX arrays at it.
   `SaveManager.UnlockedCharacters` default becomes `new(CharacterRoster.IDs)`.
2. Convert the `== 9` assertions to `== CharacterRoster.IDs.Count` / cross-checks against the manifest,
   and replace the 7 duplicated test arrays with the same source. Keep **one** test that pins the manifest
   row count so an accidental deletion still fails — but as "the manifest and the code agree", not "there
   are nine".
3. `FighterCharacterID` — record as a **deliberate, deferred** exception with a written contract
   ("append-only; new members take the next ordinal; `zoneTypeID = id*10 + slot` must stay unique").
   Do **not** attempt to de-enum it in this pass; it is the deterministic sim's identity and touching it
   risks rollback/protocol work in another workstream.
4. `AGENTS.md` — the Roster section and "all nine characters" phrasing appear ~10 times; add the mandate
   and soften the load-bearing claims.

### Status / size
**missing**. **M** for the UI/save/test seam; the sim enum is a documented deferral.

---

## 10. A10 — The two-colour visual grammar

### What the design requires
An **authored visual language, all modes and mirrors**:
- **Cold synthetic light = the Unbound** — extraction beams, siphons, neural visors, plasma; jagged,
  desaturating; *what their machines touch drains grey*.
- **Warm gold = history's resonance** — the hero's ignition and persistent aura, Chronal Dust pickups,
  restoration vignettes, the ending release. Gold always flows *from* the era *into* its people.
- **Two kinds of door:** Unbound rifts are jagged cold tears; Warden portals are clean steady geometry —
  established side-by-side in **Level 0's final beat**.
- **Recurrence contract:** every Chronal Extractor is Level 0 in miniature; every Dust pickup restates the
  origin; level-seal vignettes recolour the era warm; L14's Extraction Hall shows thin gold threads drawn
  off cold from each cradle.

### What the repo currently does
The *pigments* already exist but there is no contract and the assignment is not systematic:
- `scripts/UI/UIPalette.cs:30,33,36,42` — `Cyan (0,0.9,0.9)`, `CyanDim`, `Gold (0.95,0.8,0.3)`,
  `GoldBright (1,0.92,0.35)`. `GoldBright`'s doc comment: *"used by the Dust tiers and hyper-armor shell"* —
  so Dust is already gold (recurrence contract half-satisfied by accident).
- `scripts/Environment/ChronalExtractor.cs:198-199` — `DischargeColor = Color(0.2, 0.95, 1, 0.85)`,
  commented *"Apex Archive cyan, matching the extractor's core glow"*. Cold = enemy machine: **correct
  already**, just mislabelled (A1).
- **Level 0's fracture is violet/purple, not cold-cyan:** `Level00Controller.BuildFracturePresentation()`
  (line 525) builds the `ChronalFracture` at `Color(0.5, 0.2, 0.9, 0.85)` with a `Color(0.4,0.1,0.8,0.25)`
  halo and a `Color(0.7,0.4,1)` label. It is a generic rift, not "the Unbound's beam cracking your nexus",
  and there is **no gold ignition** and **no side-by-side Warden portal**.
- `RewindPresentationOverlay.cs` carries the monochrome/crack treatment (the desaturation vocabulary the
  grammar wants to borrow for "what their machines touch drains grey").
- `assets/shaders/outline_glow.gdshader` + `scripts/Combat/GlowPresentationController.cs` (the single
  owner of a sprite's material/tint, three independent channels) is the natural hook for a persistent
  warm-gold hero aura — it already arbitrates an outline stack.
- `StageLightingRig` (`CanvasModulate` + `PointLight2D`) exists on the ten Fighter stages, the hub and
  levels 2 and 6 only; thirteen campaign scenes are unlit. Era "recolour warm" on seal has no vehicle.

### Concrete change list
1. **Codify the contract in code, not prose.** Add `UIPalette.UnboundCold` / `UIPalette.ResonanceGold`
   (or a small `FTT.Combat.ResonanceGrammar` static) and repoint `ChronalExtractor.DischargeColor`,
   the Dust tier tints, the hero aura and the rift/portal builders at them. A `UIThemeTests`-style pin
   ("no siphon/extractor/beam site defines its own cold colour literal") is the only way this survives.
2. **Level 0 rebuilt beat sequence (design-called-out, currently absent):** cold beam cracking the nexus →
   gold ignition on the hero → the beam breaking → the Warden portal (clean geometry) beside the Unbound
   rift (jagged tear). Touches `Level00Controller.BuildFracturePresentation()` and the intro phase order.
3. **Persistent hero aura** via `GlowPresentationController` — must be a 4th arbitrated channel or a base
   tint, *not* an override that a status effect would clobber (the controller's existing three-channel
   split is designed for exactly this).
4. **L14 Extraction Hall** gold-threads-drawn-off-cold visual (A5; scene work).
5. **Level-seal vignette** warm recolour (N01's restoration vignette — cross-workstream with §3).

### Status / size
**partial**. **M** for the contract + Level 0 beat; the seal vignette and Extraction Hall ride on A5/§3.
Collides with the UI workstream in `UIPalette.cs` (`UIThemeTests` pins palette↔theme sync).

---

## 11. A11 — §1 Core Modes: Steam Remote Play Together as the launch netplay message

### What the design requires
> "**Delay-based-only netcode remains unacceptable for native online simulation.** M01 explicitly permits
> Steam Remote Play Together's host-streamed local play at launch; it is a separate delivery model."
> and (V7.6 Part 2, 2.F) "**Steam Remote Play Together is the launch netplay message** and Package 7 is
> the first post-launch beat."

### What the repo currently does
**Zero occurrences** of "Remote Play" in `scripts/`, `scenes/`, `localization/en.csv` or `project.godot`.
`scripts/Networking/NetworkManager.cs:107` has `public bool SteamTransportAvailable => false;` — the only
Steam token in the codebase, and it is about Steam Networking Sockets (Package 7), not Remote Play.
The LAN menu route was removed by V7.3 (`MainMenuSceneTests` pins its absence); `menu_lan_match` and the
`network_*` key family survive as recorded orphans (`UnusedTranslationKeyTests.RecordedOrphans`).

Remote Play Together needs no netcode — it is host-side streamed local multiplayer, so the existing
local shared-screen 1v1 path *is* the feature. The implementation consequence is a **message**, not a system.

### Concrete change list
- One new `en.csv` key on the Fighter local-play surface, e.g.
  `fighter_remote_play_notice,"Play online with a friend through Steam Remote Play Together — they join
  your local match from their own machine."` Placement: `scenes/menus/CharacterSelect.tscn` footer (it
  already hosts the Move List / Systems Card buttons and a 5-entry focus chain) or the Fighter mode entry
  on `MainMenu.tscn`. Store the raw key as the control's `text` per the repo convention.
- Whoever adds it must run `FocusChainBuilder.Apply` again if a focusable control is added, and update
  `CharacterSelectSceneTests`' focus-chain pin (currently 5 entries).
- Steam integration proper (the Remote Play Together invite API) is **out of scope**: Steamworks is not
  installed and the feature works without an in-game hook.

### Status / size
**missing**. **S**. Touches `en.csv` and one `.tscn` + its scene test.

---

## 12. A12 / A13 / A14 — smaller items

**A12 — §1 Stage Design examples.** Master now cites *"the listing deck of the Nassau Flagship, the Vesuvius
Caldera's cracking floor, the Globe Theatre stage"* and footnotes that the V6 Titanic/Apollo examples named
stages that were never authored. Repo agrees with the new text: `FighterStageCatalog` has exactly 10 stages
(`FighterStageCatalogTests.cs:18` pins `Stages.Length == 10`) and `nassau_flagship`, `vesuvius_caldera`,
`globe_theatre` are all in `SaveManager.InitialStageIDs` (lines 111–113). **Design-only, no code impact.**
Do not confuse the Fighter stage `alexandria_chambers` with the *campaign* Level 15 rename (A5) — different
content, no relationship.

**A13 — "boss fights against Unbound forces"** (§1 Adventure Mode gameplay loop) — a pure terminology
instance, covered by A1.

**A14 — the repo's `design-godot.md` mirror is stale.** Header line 3 says *"Mirror synced 2026-08-29
(V7.4)"*; the file is 3865 lines against the master's current V7.6+F-series state. The master's new header
also **drops the SYNCED-MIRROR comment block** (diff lines 2–5 are `-`), replacing it with the "Current
delivery status — 2026-09-12 (P02)" table and the "P04 authority" paragraph. Whoever re-copies should keep
a repo-local pointer comment somewhere (CLAUDE.md's authority map already names the master path) so the
"do not edit here" rule survives. **S**, but it is a single 3865-line file every workstream will want to
touch — recommend one agent owns the re-copy at the end of the batch, not per-workstream edits.

---

## 13. Cross-cutting notes for the batch coordinator

1. **`localization/en.csv` is the contention point.** This workstream alone rewrites ~120 rows and adds
   ~20. Recommend: this workstream owns the whole file for one serialized slot, or every agent appends
   only and a single pass reconciles. Duplicate-key detection lives in
   `EnemyRosterContentTests.EnglishTranslationTableHasNoDuplicateKeys`.
2. **Always finish with `--headless --import` and commit the regenerated `localization/en.en.translation`.**
   `--headless --quit` does not recompile it; `CampaignLocalizationTests` translates through the compiled
   resource and will fail the whole batch otherwise. (CLAUDE.md documents this trap explicitly.)
3. **Key names should not change.** Every rename I considered (`neo_earth_*` → `bastion_*`,
   `alexandria_*` → `founding_*`, `apex_eraser` → `first_unbound`) is forbidden by the design's
   legacy-ID retention rule and would cascade into `Level14/15ContentTests`, `ScenePoolConfigTests`,
   `StoryAudioSetContentTests`, `LoadingScreenTests`, `RosterVfxMappingTests`, `MainMenu.tscn:707`, sprite
   atlas paths and the manifest. Values-only is both correct and cheap.
4. **Do not run GdUnit concurrently across worktrees** (CLAUDE.md failure signature 5). This workstream's
   verification is `dotnet build` + `--headless --import` + the localization/content suites by `--filter`;
   only the batch closeout needs the full 1638 run.
5. **One `[TestSuite]` per file** for any new suite (failure signature 6), and never
   `OverrideFailureMessage("")` on an empty error list — use the repo idiom
   `if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");`.
6. **Contradictions to surface rather than normalize:** `dlg_l14_exit_2` ("nobody down there to bring
   home… no locals, no coerced crews, nothing to restore") vs. the Extraction Cradles canon; and the
   Prime Anchor's inverted polarity (restoration stone → the Unbound's weapon) sharing the same words as
   the shipped `alexandria_anchor_*` / `alexandria_objective_restore` copy.
7. **Naming collision already ruled:** enemy `chrono_warden` / "Chrono-Warden" is **retained** per master
   line 2948 ("a stolen title worn mockingly") despite the Wardens now being the player's faction.
   No rename, no test change — just a lore comment.
