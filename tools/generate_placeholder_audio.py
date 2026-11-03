#!/usr/bin/env python3
"""Generates the placeholder one-shot SFX under ``audio/sfx/``.

Most files render as digital silence (see ``ASSETS`` below: each asset carries its own
audible flag); the tone builders of the silent ones are kept as the shape reference for
what each cue meant. Since 2026-11-03 (GR-AUDIO, ruling 13 of the gameplay review) eight
combat cues are audible: short, dry, quiet placeholders with a peak ceiling of
``AUDIBLE_PEAK_CEILING``. Everything is original synthesis from numpy; no third-party
material. The assets exist so the cue call sites that already reference them stop
no-oping -- notably ``FighterSimulationDriver``'s ``ko_stinger.ogg`` /
``victory_fanfare.ogg`` constants, which resolve through ``ResourceLoader.Exists`` and
silently skip when the file is absent. Production audio replaces the files in place; no
code change.

Ogg Vorbis (not WAV) because the driver's paths are authored with the ``.ogg``
extension and are owned by another workstream.

Usage (from the repository root):

    python tools/generate_placeholder_audio.py            # every asset
    python tools/generate_placeholder_audio.py hit_basic.ogg block.ogg   # only those

The manifest ``audio/sfx/placeholder_cue_manifest.json`` is rewritten on every run: it
states each asset's audible flag, peak and length for the content tests (a Vorbis stream
cannot be decoded in the test host).

Then run a Godot headless ``--import`` so the ``.import`` sidecars are written,
and commit the ``.ogg`` files together with their ``.import`` files.

Requires ``numpy`` and ``soundfile`` (libsndfile provides the Vorbis encoder):

    python -m pip install numpy soundfile
"""

from __future__ import annotations

import json
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

# 2026-08-09: the placeholder cues rendered as digital silence by directive -- the
# synthesized tones were judged distracting in playtests, matching the muting of
# the three placeholder music stems the same day. Every cue keeps its exact
# duration, file path, and non-looping import settings, so call sites, .import
# sidecars, and PlaceholderCueAssetTests (path, duration bounds, Loop == false)
# hold unchanged, and production audio still drops in with zero code changes.
#
# 2026-11-03 (gameplay review ruling 13, GR-AUDIO): the directive is amended for
# eight combat cues only. The silence is now a per-asset flag (the ``audible`` field of
# ``ASSETS``); every other asset stays silent. The audible ones are short and dry
# (no sustained tones, no beeps), peak at most ``AUDIBLE_PEAK_CEILING`` here and are
# played a further 12 dB down by their catalog rows, and the player can mute them
# separately in Settings (``GlobalSaveData.PlaceholderCuesMuted``).
AUDIBLE_PEAK_CEILING = 0.3
MANIFEST_PATH = "audio/sfx/placeholder_cue_manifest.json"


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
    """Short, heavy two-stage downward hit. Scaled under the ceiling when audible."""
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


def rewind_sweep() -> np.ndarray:
    """Package 8 B6: the Chronal Rewind reverse sweep.

    Filtered noise swelling into a downward tone, so it reads as "time running
    backwards" rather than as a hit. Seeded so regenerating the file byte-matches.
    """
    seconds = 0.9
    length = int(seconds * SAMPLE_RATE)
    rng = np.random.default_rng(20260808)
    noise = rng.standard_normal(length)
    # Cheap one-pole low pass whose cutoff closes over the sweep.
    filtered = np.empty(length, dtype=np.float64)
    state = 0.0
    alpha = np.linspace(0.28, 0.02, length)
    for index in range(length):
        state += alpha[index] * (noise[index] - state)
        filtered[index] = state
    filtered /= max(1e-9, float(np.max(np.abs(filtered))))
    swell = np.concatenate([
        np.linspace(0.0, 1.0, int(length * 0.65)),
        np.linspace(1.0, 0.0, length - int(length * 0.65)),
    ])
    body = tone(320.0, seconds, amplitude=0.10, harmonic=0.2, attack=0.25, release=0.4, bend=0.35)
    return filtered * swell * 0.16 + body


def clock_tick() -> np.ndarray:
    """Package 8 B6: one tick of the rewind clock. Short, dry, unobtrusive."""
    return tone(1480.0, 0.045, amplitude=0.09, harmonic=0.5, attack=0.001, release=0.03, bend=0.8)


# --- GR-AUDIO (2026-11-03): the seven new combat cues. Short, dry, seeded noise so a
# regenerated file byte-matches. Built from filtered noise bursts, fast-decaying
# partials and short sweeps rather than sustained tones, so none reads as a beep.


def _noise(seconds: float, seed: int) -> np.ndarray:
    return np.random.default_rng(seed).standard_normal(int(seconds * SAMPLE_RATE))


def _one_pole_low(signal: np.ndarray, cutoff_hz: float) -> np.ndarray:
    alpha = 1.0 - math.exp(-2.0 * math.pi * cutoff_hz / SAMPLE_RATE)
    out = np.empty_like(signal)
    state = 0.0
    for index in range(signal.size):
        state += alpha * (signal[index] - state)
        out[index] = state
    return out


def _decay(length: int, seconds: float) -> np.ndarray:
    """Exponential decay envelope with a 1 ms attack so nothing clicks."""
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    env = np.exp(-t / max(1e-6, seconds))
    attack = min(length, max(1, int(0.001 * SAMPLE_RATE)))
    env[:attack] *= np.linspace(0.0, 1.0, attack)
    return env


def _partial(freq: float, length: int, decay_seconds: float, amplitude: float) -> np.ndarray:
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    return np.sin(2.0 * math.pi * freq * t) * _decay(length, decay_seconds) * amplitude


def _mix(*layers: np.ndarray) -> np.ndarray:
    out = np.zeros(max(layer.size for layer in layers), dtype=np.float64)
    for layer in layers:
        out[:layer.size] += layer
    return out


def _fade_tail(signal: np.ndarray, seconds: float = 0.012) -> np.ndarray:
    n = min(signal.size, int(seconds * SAMPLE_RATE))
    signal[signal.size - n:] *= np.linspace(1.0, 0.0, n)
    return signal


def hit_basic() -> np.ndarray:
    """A dry thump: a very short low-passed noise burst over a 180 Hz body that drops fast."""
    length = int(0.09 * SAMPLE_RATE)
    burst = _one_pole_low(_noise(0.09, 11), 3200.0) * _decay(length, 0.012) * 0.55
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    phase = 2.0 * math.pi * np.cumsum(180.0 * np.exp(-t / 0.05) + 70.0) / SAMPLE_RATE
    body = np.sin(phase) * _decay(length, 0.03) * 0.7
    return _fade_tail(_mix(burst, body))


def block() -> np.ndarray:
    """A short inharmonic metallic ping on a one-millisecond click."""
    length = int(0.12 * SAMPLE_RATE)
    click = _one_pole_low(_noise(0.12, 23), 6000.0) * _decay(length, 0.004) * 0.35
    ping = (_partial(1130.0, length, 0.030, 0.45) + _partial(1710.0, length, 0.022, 0.28)
            + _partial(2540.0, length, 0.016, 0.18))
    return _fade_tail(_mix(click, ping))


def perfect_block() -> np.ndarray:
    """Block's ping a fifth brighter, with a short shimmer of two close high partials beating."""
    length = int(0.20 * SAMPLE_RATE)
    click = _one_pole_low(_noise(0.20, 29), 8000.0) * _decay(length, 0.003) * 0.28
    ping = (_partial(1700.0, length, 0.050, 0.40) + _partial(2550.0, length, 0.040, 0.26)
            + _partial(3820.0, length, 0.030, 0.16))
    shimmer = _partial(5120.0, length, 0.060, 0.12) + _partial(5190.0, length, 0.060, 0.12)
    return _fade_tail(_mix(click, ping, shimmer))


def swing_whiff() -> np.ndarray:
    """A band-limited noise sweep (a state-variable band-pass falling 2.2 kHz to 700 Hz)."""
    seconds = 0.14
    length = int(seconds * SAMPLE_RATE)
    noise = _noise(seconds, 31)
    centre = np.linspace(2200.0, 700.0, length)
    q = 0.35
    low = band = 0.0
    out = np.empty(length, dtype=np.float64)
    for index in range(length):
        f = 2.0 * math.sin(math.pi * centre[index] / SAMPLE_RATE)
        low += f * band
        high = noise[index] - low - q * band
        band += f * high
        out[index] = band
    out /= max(1e-9, float(np.max(np.abs(out))))
    swell = np.sin(np.linspace(0.0, math.pi, length)) ** 1.5
    return _fade_tail(out * swell * 0.45)


def grab_catch() -> np.ndarray:
    """Fabric and armour: a low-passed noise grip, a 95 Hz thud and a faint buckle tick."""
    length = int(0.14 * SAMPLE_RATE)
    grip = _one_pole_low(_noise(0.14, 37), 1800.0) * _decay(length, 0.035) * 0.6
    thud = _partial(95.0, length, 0.05, 0.6)
    tick = _partial(2100.0, length, 0.008, 0.18)
    return _fade_tail(_mix(grip, thud, tick))


def defy_burst() -> np.ndarray:
    """A crack, a falling low sweep and a short cracked-bell toll (three inharmonic partials)."""
    length = int(0.45 * SAMPLE_RATE)
    crack = _one_pole_low(_noise(0.45, 41), 5000.0) * _decay(length, 0.010) * 0.45
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    phase = 2.0 * math.pi * np.cumsum(220.0 * np.exp(-t / 0.09) + 55.0) / SAMPLE_RATE
    sweep = np.sin(phase) * _decay(length, 0.12) * 0.7
    bell = (_partial(660.0, length, 0.16, 0.22) + _partial(1043.0, length, 0.12, 0.14)
            + _partial(1782.0, length, 0.08, 0.09))
    return _fade_tail(_mix(crack, sweep, bell), 0.03)


def ultimate_windup() -> np.ndarray:
    """A rising warning sweep, 300 to 950 Hz over the 20-frame wind-up, swelling then cut; a soft flutter."""
    seconds = 0.33
    length = int(seconds * SAMPLE_RATE)
    t = np.arange(length, dtype=np.float64) / SAMPLE_RATE
    freq = 300.0 + 650.0 * (t / seconds) ** 1.6
    phase = 2.0 * math.pi * np.cumsum(freq) / SAMPLE_RATE
    wave = np.sin(phase) + 0.25 * np.sin(2.0 * phase)
    flutter = 0.8 + 0.2 * np.sin(2.0 * math.pi * 22.0 * t)
    swell = np.linspace(0.0, 1.0, length) ** 1.3
    return _fade_tail(wave * flutter * swell * 0.4, 0.02)


# path -> (builder, audible). Only the eight audible assets make a sound; the rest stay
# the digital silence of the 2026-08-09 directive.
ASSETS = {
    "audio/sfx/combat/ko_stinger.ogg": (ko_stinger, True),
    "audio/sfx/ui/victory_fanfare.ogg": (victory_fanfare, False),
    "audio/sfx/ui/countdown_blip.ogg": (countdown_blip, False),
    "audio/sfx/chronal/rewind_sweep.ogg": (rewind_sweep, False),
    "audio/sfx/chronal/clock_tick.ogg": (clock_tick, False),
    "audio/sfx/combat/hit_basic.ogg": (hit_basic, True),
    "audio/sfx/combat/block.ogg": (block, True),
    "audio/sfx/combat/perfect_block.ogg": (perfect_block, True),
    "audio/sfx/combat/swing_whiff.ogg": (swing_whiff, True),
    "audio/sfx/combat/grab_catch.ogg": (grab_catch, True),
    "audio/sfx/combat/defy_burst.ogg": (defy_burst, True),
    "audio/sfx/combat/ultimate_windup.ogg": (ultimate_windup, True),
}


def render(builder, audible: bool) -> np.ndarray:
    """The asset's samples: its tone scaled under the ceiling, or zeros of the same length."""
    samples = builder()
    peak = float(np.max(np.abs(samples)))
    if not audible:
        if peak > 0.5:
            sys.exit(f"a silent asset's shape reference peaks at {peak:.3f}; keep references quiet")
        # Preserve the sample count (and therefore GetLength()) exactly.
        return np.zeros_like(samples)
    if peak > AUDIBLE_PEAK_CEILING:
        samples = samples * (AUDIBLE_PEAK_CEILING / peak)
    return samples


def main(argv: list[str]) -> int:
    """Writes the assets named in ``argv`` (a file name or path suffix), or all of them."""
    manifest = {}
    for relative_path, (builder, audible) in ASSETS.items():
        samples = render(builder, audible)
        peak = float(np.max(np.abs(samples)))
        manifest[relative_path] = {
            "audible": audible,
            "peak": round(peak, 4),
            "seconds": round(samples.size / SAMPLE_RATE, 4),
        }
        if argv and not any(relative_path.endswith(name) for name in argv):
            continue
        target = os.path.join(REPO_ROOT, relative_path)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        sf.write(target, samples.astype(np.float32), SAMPLE_RATE, format="OGG", subtype="VORBIS")
        print(f"wrote {relative_path} ({samples.size / SAMPLE_RATE:.2f}s, peak {peak:.3f}, "
              f"{'audible' if audible else 'silent'})")
    # The manifest is what the content tests read: a Vorbis stream cannot be decoded in the
    # test host, so the generator states each asset's peak and length.
    with open(os.path.join(REPO_ROOT, MANIFEST_PATH), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(manifest, handle, indent=2, sort_keys=True)
        handle.write("\n")
    print(f"wrote {MANIFEST_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
