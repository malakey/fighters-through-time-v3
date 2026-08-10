# Placeholder audio kit

These `AudioStreamWAV` resources are the Package 0 test audio kit: 8-bit mono PCM streams that stand in for production music stems and SFX while proving the final routing integration points (bus assignment, layered stem playback, crossfades, ducking, pooling, one-shot SFX).

As of 2026-08-09 every payload in this kit is digital silence by directive — the synthesized placeholder tones were judged distracting in playtests. Loop points, mix rates, formats, durations, and byte counts are unchanged, so every routing/sync contract and test still exercises the real pipeline; do not re-add audible tones. The five generated one-shot cues under `audio/sfx/` are silent the same way (`tools/generate_placeholder_audio.py`, `RENDER_SILENT`).

- `placeholder_stem_*.tres` are one-second forward loops (now silent) that ambient/combat/boss layering and transitions play through.
- `placeholder_sfx_*.tres` are short non-looping one-shots (now silent) with per-category durations.
- `placeholder_tone.tres` is the original minimal loop retained for compatibility (also silent).

Production replacements must arrive as per-level/per-stage audio set resources at the paths listed in `resources/Content/content_manifest.csv` (`AudioSet` entries), keeping bus routing, loop points, and sync behavior contracts intact. Replacing these kit files must not require gameplay code changes.
