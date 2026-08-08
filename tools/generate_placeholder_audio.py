#!/usr/bin/env python3
"""Generates the Package 8 A2 placeholder one-shot SFX under ``audio/sfx/``.

These are deliberately crude synthesized tones: distinct enough to tell apart in
a playtest, quiet enough not to fatigue, and obviously not production audio.
They exist so the cue call sites that already reference them stop no-oping —
notably ``FighterSimulationDriver``'s ``ko_stinger.ogg`` / ``victory_fanfare.ogg``
constants, which resolve through ``ResourceLoader.Exists`` and silently skip when
the file is absent. Production audio replaces the files in place; no code change.

Ogg Vorbis (not WAV) because the driver's paths are authored with the ``.ogg``
extension and are owned by another workstream.

Usage (from the repository root):

    python tools/generate_placeholder_audio.py

Then run a Godot headless ``--import`` so the ``.import`` sidecars are written,
and commit the ``.ogg`` files together with their ``.import`` files.

Requires ``numpy`` and ``soundfile`` (libsndfile provides the Vorbis encoder):

    python -m pip install numpy soundfile
"""

from __future__ import annotations

import math
import os
import sys

try:
    import numpy as np
    import soundfile as sf
except ImportError:  # pragma: no cover - developer tooling
    sys.exit("This script needs numpy and soundfile: python -m pip install numpy soundfile")

SAMPLE_RATE = 44100
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def envelope(length: int, attack: float, release: float) -> np.ndarray:
    """Linear attack / exponential release, so nothing clicks at either end."""
    attack_samples = max(1, int(attack * SAMPLE_RATE))
    release_samples = max(1, int(release * SAMPLE_RATE))
    env = np.ones(length, dtype=np.float64)
    attack_samples = min(attack_samples, length)
    env[:attack_samples] = np.linspace(0.0, 1.0, attack_samples)
    release_samples = min(release_samples, length)
    env[length - release_samples:] *= np.exp(np.linspace(0.0, -6.0, release_samples))
    return env


def tone(freq: float, seconds: float, *, amplitude: float, harmonic: float = 0.0,
         attack: float = 0.004, release: float = 0.08, bend: float = 1.0) -> np.ndarray:
    """A sine with an optional octave harmonic and an optional linear pitch bend."""
    length = int(seconds * SAMPLE_RATE)
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    # Integrate the swept frequency so the bend does not phase-discontinuity.
    sweep = np.linspace(1.0, bend, length)
    phase = 2.0 * math.pi * np.cumsum(freq * sweep) / SAMPLE_RATE
    wave = np.sin(phase)
    if harmonic > 0.0:
        wave += harmonic * np.sin(2.0 * phase)
    wave /= 1.0 + harmonic
    return wave * envelope(length, attack, release) * amplitude


def silence(seconds: float) -> np.ndarray:
    return np.zeros(int(seconds * SAMPLE_RATE), dtype=np.float64)


def ko_stinger() -> np.ndarray:
    """Short, heavy two-stage downward hit. Peaks well under full scale."""
    impact = tone(220.0, 0.18, amplitude=0.30, harmonic=0.45, attack=0.001, release=0.12, bend=0.55)
    body = tone(110.0, 0.45, amplitude=0.22, harmonic=0.25, attack=0.002, release=0.35, bend=0.7)
    out = np.zeros(max(impact.size, body.size), dtype=np.float64)
    out[:impact.size] += impact
    out[:body.size] += body
    return out


def victory_fanfare() -> np.ndarray:
    """Rising major triad; reads as "you won" without pretending to be music."""
    notes = [(523.25, 0.20), (659.25, 0.20), (783.99, 0.55)]
    parts = [tone(freq, seconds, amplitude=0.18, harmonic=0.3, release=0.18)
             for freq, seconds in notes]
    return np.concatenate(parts)


def countdown_blip() -> np.ndarray:
    """One tick of the pre-match countdown. Pitch-shifted at play time for GO."""
    return tone(880.0, 0.10, amplitude=0.16, harmonic=0.2, attack=0.002, release=0.05)


ASSETS = {
    "audio/sfx/combat/ko_stinger.ogg": ko_stinger,
    "audio/sfx/ui/victory_fanfare.ogg": victory_fanfare,
    "audio/sfx/ui/countdown_blip.ogg": countdown_blip,
}


def main() -> int:
    for relative_path, builder in ASSETS.items():
        samples = builder()
        peak = float(np.max(np.abs(samples)))
        if peak > 0.5:
            sys.exit(f"{relative_path} peaks at {peak:.3f}; placeholders must stay quiet")
        target = os.path.join(REPO_ROOT, relative_path)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        sf.write(target, samples.astype(np.float32), SAMPLE_RATE, format="OGG", subtype="VORBIS")
        print(f"wrote {relative_path} ({samples.size / SAMPLE_RATE:.2f}s, peak {peak:.3f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
