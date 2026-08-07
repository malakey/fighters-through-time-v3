# Placeholder audio kit

These `AudioStreamWAV` resources are the Package 0 test audio kit: 8-bit mono PCM tones that stand in for production music stems and SFX while proving the final routing integration points (bus assignment, layered stem playback, crossfades, ducking, pooling, one-shot SFX).

- `placeholder_stem_*.tres` are one-second forward loops with distinct pitch and pulse cadence so ambient/combat/boss layering and transitions are audibly distinguishable during testing.
- `placeholder_sfx_*.tres` are short non-looping decaying one-shots with distinct pitches per category.
- `placeholder_tone.tres` is the original minimal loop retained for compatibility.

Production replacements must arrive as per-level/per-stage audio set resources at the paths listed in `resources/Content/content_manifest.csv` (`AudioSet` entries), keeping bus routing, loop points, and sync behavior contracts intact. Replacing these kit files must not require gameplay code changes.
