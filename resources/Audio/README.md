# Placeholder audio kit

These `AudioStreamWAV` resources are the Package 0 test audio kit: 8-bit mono PCM streams that stand in for production music stems and SFX while proving the final routing integration points (bus assignment, layered stem playback, crossfades, ducking, pooling, one-shot SFX).

As of 2026-08-09 every payload in this kit is digital silence by directive — the synthesized placeholder tones were judged distracting in playtests. Loop points, mix rates, formats, durations, and byte counts are unchanged, so every routing/sync contract and test still exercises the real pipeline; do not re-add audible tones. The five generated one-shot cues under `audio/sfx/` are silent the same way (`tools/generate_placeholder_audio.py`, `RENDER_SILENT`).

- `placeholder_stem_*.tres` are one-second forward loops (now silent) that ambient/combat/boss layering and transitions play through.
- `placeholder_sfx_*.tres` are short non-looping one-shots (now silent) with per-category durations.
- `placeholder_tone.tres` is the original minimal loop retained for compatibility (also silent).

## Amendment, 2026-11-03: eight audible combat cues (GR-AUDIO)

Gameplay review ruling 13 amends the silence directive **for these cues only**. The player gets a small hit-sound set before the first hands-on session: `hit_basic`, `block`, `perfect_block`, `swing_whiff`, `grab_catch`, `ko_stinger` (and its `ko_stinger_decisive` variant, which shares the asset and replaces it on the match-ending KO), `defy_burst` and `ultimate_activation_windup`. They live in `core_mechanic_cues.tres` and are still placeholders until Package 10:

- **Original synthesis, no third-party material.** `tools/generate_placeholder_audio.py` builds each from numpy (filtered noise, fast-decaying partials, short sweeps). Silence is now a per-asset flag (`ASSETS`: the builder and an `audible` boolean); every other `.ogg` and every `.tres` payload in this kit is unchanged digital silence. Do not re-add audible tones elsewhere.
- **Short, dry and quiet.** Each is 0.09-0.45 s, peaks at no more than 0.3 (`AUDIBLE_PEAK_CEILING`), and its catalog row plays it a further 12 dB down (`CoreMechanicCueEntry.VolumeDb`, additive, default 0). `audio/sfx/placeholder_cue_manifest.json`, rewritten on every run, states each asset's audible flag, peak and length, because the test host cannot decode Vorbis; `PlaceholderAudioKitTests` and `CoreMechanicCueCatalogTests` read it.
- **Throttled.** `CoreMechanicCueEntry.MinIntervalSeconds` (additive, default 0) drops a second request for the same row inside its window (hit 0.09 s, block 0.12, perfect block 0.15, whiff 0.15), so a swarm wave cannot machine-gun a cue.
- **Separately mutable.** `GlobalSaveData.PlaceholderCuesMuted` (additive, no schema bump) with a Settings row (Audio tab) skips exactly the cues in `CoreMechanicCueIDs.AudiblePlaceholders`, read through `FTT.Core.PlaceholderCueSettings`. It is independent of the SFX category mute.

Production replacements must arrive as per-level/per-stage audio set resources at the paths listed in `resources/Content/content_manifest.csv` (`AudioSet` entries), keeping bus routing, loop points, and sync behavior contracts intact. Replacing these kit files must not require gameplay code changes.
