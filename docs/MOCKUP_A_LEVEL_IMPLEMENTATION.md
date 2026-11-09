# Mockup A Level Implementation

The implementation contains 47 registered PNG assets: 16 story plates, the original level 00 study image, and 10 each of Fighter Mode base plates, backdrops, and foreground mattes. The 26 gallery pairs cover all 16 story openings and 10 Fighter arenas, each mapped to its selected A reference in `D:/Projects/fighters-through-time-docs-3/mockups`; the Gettysburg Fighter arena uses `A_v2.png`. Story progression beyond the opening compositions largely preserves existing geometry, with the approved story art supplying the source-style backdrop and terrain. Every PNG has a matching `.provenance.json`; [`assets/environments/mockup_a/source-manifest.json`](../assets/environments/mockup_a/source-manifest.json) aggregates those records with unique IDs for the distinct fighter layers while retaining their shared source references.

## Story openings

`StoryOpeningProfile`, `StoryOpeningPresentation`, and `StoryOpeningFigureScale` provide level-specific opening art, figure size, geometry, presentation, and camera behavior. `resources/Presentation/story_openings.json` maps all 16 levels to registered 800 × 450 artwork, surfaces, spawn, and release point. At the initial composition, the camera and opening figure are placed to match each approved A image and its painted platform geometry. During the frozen 2.5-second level 00 cold open, the seated-Einstein study art is shown only when Einstein is selected; other selected heroes remain live. Live Einstein is restored before the existing Hold, Rift, and BreakTheBeam sequence.

The story camera keeps the hero and HUD within the usable frame during the opening and release, with robust camera-center handling for rounding and excess span. Story levels use registered profile colliders aligned with their artwork. Fighter scene collision geometry matches the canonical FighterStageGeometry definitions. Levels 14 and 15 are **Severed Bastion** and **Meridian Founding**.

## Fighter Mode

`FighterStageVisualProfile` maps the ten approved stages into a canonical 960-unit world width at 62.5 pixels per simulation unit. The ten profile entries mirror the ten implemented Fighter stage scenes. The legacy `berlin_wall` identifier and scene depict Hakata Seawall.

Each fighter stage draws its environment backdrop, base art, and separately registered transparent foreground matte. The controller transitions from the full plate to its foreground matte over 0.15 seconds after the camera frame departs (zoom change greater than 0.015 or center shift greater than 1.5 world pixels). It returns to the full plate only when the camera is near the original frame (zoom difference below 0.005 and center shift below 0.5 world pixels), providing hysteresis. The foreground edges use a 12-world-pixel feather. 

CPU recovery prioritizes jumping when it is unsupported and descending below the reachable ledge-capture band, while jumps remain. A guard prevents a horizontal dash with no lift from taking priority; existing lifting ability behavior and reaction delay remain in effect.

## Validation

`LevelOpeningCaptureRunner` requires a rendering backend and runs without `--headless`. After the scene argument, pass either `--story=0..15` or `--fighter=stage_id`, plus optional `--hero=einstein`, `--opponent=joan`, `--settle=12`, and `--out=directory`; `--hide-ui` is also available. Story relocation diagnostics accept `--story-x=worldX`, optional `--story-y=worldY`, and `--relocate-settle=180`.

All 47 art imports exited successfully and the project build exited successfully. The final full suite reported 4,298 passed, 0 failed, and 0 skipped in 2 minutes 40 seconds (`artifacts/validation/test-level-update-round6-full-retry.log`); the focused camera suite passed 11/11 (`artifacts/validation/test-level-update-round6-camera-focused.log`). All 26 gallery captures, plus separate shifted-camera and high-platform probes, were verified. Relocation diagnostics check camera relocation; they do not establish traversal coverage. Movement behavior was exercised by the automated suite. Visual review is complete: all 26 opening captures and the reviewed fighter captures were accepted, including the final elevated-camera checks.

Open [`artifacts/mockup_a_validation/index.html`](../artifacts/mockup_a_validation/index.html) for the reference-versus-capture gallery. It pairs each of the 16 clean story opening captures and 10 Fighter arena captures with its selected A reference and links to the corresponding source PNG and capture artifact. The seated cinematic study is not used as gameplay art.

## After the gameplay review package

The openings replace the generated collision in a level's first 800 px, and the package's traversal stretches on Levels 7 and 13 begin inside that frame, so those two profiles state the stretch's collision. Level 7's plate still draws the pier ending at x 322, so its first screen no longer matches its collision (ledger `VERIFY-MOCKUP-OPENING-L07-ART`).
