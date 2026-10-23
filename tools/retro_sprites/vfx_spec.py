"""Ability VFX and construct art for the retro v2 pass (2026-10-18).

One VFX atlas per character: rows special_1, special_2, movement, ultimate,
three frames each. The same row serves as the cast flash, the impact flash, a
projectile's body and a zone's body, so frame 1 is always the sustained "body"
of the effect (what a projectile shows in flight), frame 0 its onset and frame
2 its burst or resolve. Rows that loop in game (special_2 and ultimate) are
three phases of a cycle at full size, so the loop never pops from small to
large. Every effect travels or faces screen-right. Descriptions follow the
current (Package 13) kits.
"""

from __future__ import annotations

VFX_FRAMES: dict[str, list[list[str]]] = {
    "einstein": [
        ["a small apple-sized sphere of crackling cyan energy forming, sparks around it",
         "the glowing cyan energy sphere flying right with a short streaming tail of light behind it",
         "a blinding cyan-white detonation burst with a shockwave ring"],
        ["a sphere of warped violet-and-cyan spacetime: bending grid lines, chromatic fringes",
         "the same distortion sphere, its grid lines rotated a third of a turn",
         "the same distortion sphere, rotated two thirds of a turn, ripples pulsing"],
        ["a vertical tear of folded spacetime opening, cyan and violet light",
         "the fold wide open, ribbons of light spiralling into it",
         "the fold collapsing into a scatter of cyan sparkles"],
        ["a black singularity with a swirling violet accretion disk of glowing cyan chalk equations",
         "the same singularity, the disk rotated a third of a turn, symbols streaming inward",
         "the same singularity, rotated further, a pulse of violet light at its rim"],
    ],
    "joan": [
        ["golden holy light erupting from a point on the ground",
         "a low wave of golden holy fire rolling right along the ground",
         "the wave cresting and bursting into golden sparks"],
        ["three rapid golden thrust streaks like lances of light pointing right",
         "a different trio of golden thrust streaks pointing right, sparks at the tips",
         "a third trio of golden thrust streaks pointing right, a starburst at the front"],
        ["burning golden ethereal wings beginning to unfurl",
         "fully spread flaming golden wings",
         "the wings dissolving into rising golden embers"],
        ["a charging line of glowing spectral golden knights on horseback galloping right, banners raised",
         "the same spectral cavalry mid-stride, hooves kicking golden dust",
         "the same spectral cavalry at full gallop, lances lowered"],
    ],
    "leonardo": [
        ["a small glowing golden spiral appearing",
         "a large glowing golden Fibonacci spiral with ochre paint sparks",
         "the spiral fully expanded and fading into golden motes"],
        ["a brass crossbow bolt launching right in a puff of gear smoke",
         "a brass crossbow bolt flying right with a short golden trail",
         "the bolt striking with a burst of brass splinters and tiny gears"],
        ["a puff of air and canvas scraps bursting outward",
         "a sweeping gust of air streaming right with feathers and canvas scraps",
         "a fading curl of wind"],
        ["a glowing golden Vitruvian circle-and-square geometric sphere with blueprint-blue lines and turning gears",
         "the same geometric sphere, rings rotated a third of a turn",
         "the same geometric sphere, rotated further, gears spinning"],
    ],
    "lincoln": [
        ["an earthen burst of dust and wood splinters at ground level",
         "a low golden-amber shockwave of cracked earth racing right along the ground",
         "the wave erupting into flying rocks and splinters"],
        ["a huge amber crescent slash arc sweeping down, splinters flying",
         "the same crescent arc a moment later, brighter at its leading edge",
         "the same crescent arc fading, splinters scattering"],
        ["a burst of cyan and amber speed lines",
         "a horizontal ram wake streaming right: speed lines and kicked-up dust",
         "the wake fading into dust"],
        ["a row of split-rail fence posts erupting from the ground in golden light",
         "the same fence posts, taller, golden light flaring between them",
         "the same fence posts, splinters and light bursting around them"],
    ],
    "cleopatra": [
        ["a swirl of golden sand opening on the ground",
         "glowing teal spectral cobras rising from a nest of golden sand",
         "the cobras striking forward, venom glints"],
        ["a rotating vortex of golden sand with teal glints",
         "the same vortex, rotated a third of a turn",
         "the same vortex, rotated two thirds of a turn"],
        ["a burst of golden sand",
         "a streaming cloud of golden sand rushing right",
         "sand settling into a soft haze"],
        ["a giant glowing teal spectral cobra rearing out of a golden sandstorm",
         "the same cobra striking forward, sand swirling",
         "the same cobra recoiling, sand and teal light swirling"],
    ],
    "tesla": [
        ["a burst of blue-white sparks at ground level",
         "crackling blue-white lightning arcs radiating outward",
         "the arcs fading into sparks"],
        ["an expanding ring of blue electromagnetic force with curved dipole field lines",
         "the same ring pulsing outward, field lines shifted",
         "the same ring pulsing again, field lines shifted further"],
        ["a cluster of crackling blue-white sparks",
         "a horizontal streak of pure blue-white lightning racing right",
         "crackling sparks dispersing"],
        ["a spectral violet-blue Wardenclyffe tower silhouette crackling with lightning",
         "the same tower, a column of violet lightning striking down beside it",
         "the same tower, lightning webbing outward"],
    ],
    "shakespeare": [
        ["a pale skull wreathed in ghostly blue wisps",
         "the pale skull flying right trailing ghostly blue wisps",
         "a ghostly blue wailing ring bursting around the skull"],
        ["a swirling blue-grey wind storm with leaves and scraps of inked paper",
         "the same storm, rotated a third of a turn",
         "the same storm, rotated two thirds of a turn"],
        ["a magical gust gathering in a spiral",
         "a strong swirling magical gust sweeping up and to the right",
         "the gust dissipating into faint swirls"],
        ["spectral blue theatrical phantoms (a crowned king, a masked lady, a raised dagger) circling within a ring of glowing ink script",
         "the same phantoms, the ring rotated a third of a turn",
         "the same phantoms, rotated further, ink script flaring"],
    ],
    "mozart": [
        ["glowing blue-and-gold musical notes gathering",
         "a cluster of glowing blue-gold chord notes streaking right",
         "a sonic ring burst with notes scattering"],
        ["a towering curved wall of blue-gold sound waves with musical notes, rolling right",
         "the same wall of sound, its waves shifted forward",
         "the same wall of sound, waves shifted further"],
        ["a few glowing notes rising",
         "a rising arc of glowing blue-gold notes (a glissando)",
         "a short glowing five-line music staff floating horizontally"],
        ["glowing ivory-and-gold piano keys falling like meteors with blue streaks",
         "more piano keys falling, some shattering into notes",
         "piano keys raining down, a burst of notes below"],
    ],
    "tubman": [
        ["a flare of warm lantern light at ground level",
         "a line of pale-blue spectral Union scout silhouettes running right along the ground",
         "the scouts dissolving into warm light"],
        ["a shimmering gold North Star halo with a bright star glint",
         "the same halo, the glint shifted around the ring",
         "the same halo, the glint shifted further"],
        ["a bright gold star flare",
         "a streak of gold starlight arcing up and to the right",
         "star sparkles fading"],
        ["a spectral locomotive made of warm gold-white lantern light roaring right, trailing light",
         "the same spectral train, wheels and steam of light churning",
         "the same spectral train, a burst of lantern light at its front"],
    ],
}

ROW_NAMES = ["special ability 1", "special ability 2", "movement ability", "ultimate"]

CONSTRUCTS: dict[str, str] = {
    "tesla_coil": "a compact copper Tesla coil on a riveted brass base with a glass bulb on top, small blue-white sparks crackling around the bulb",
    "clockwork_turret": "a small brass-and-wood clockwork crossbow turret on a squat tripod, a loaded bolt pointing right, visible gears and a winding key",
    "serpent_nest": "a low mound of golden sand ringed with carved Egyptian stones, three glowing teal spectral cobras coiled and rearing from it",
    "sonata_platform": "a short horizontal floating platform shaped like a glowing blue-and-gold five-line music staff with a treble clef and a few notes",
}


def vfx_prompt(character: str, name: str) -> str:
    rows = VFX_FRAMES[character]
    lines = [
        "Use case: stylized-concept",
        "Asset type: 2D game ability visual-effect sprite sheet (no characters)",
        "Canvas: portrait 2:3. An invisible grid of 4 rows and 3 equal columns, read left to "
        "right, top row first. Each row is one effect's three animation frames; each frame is "
        "centred in its own cell with clear empty space around it and never crosses into a "
        "neighbouring cell. Within a row all three frames share one scale.",
        f"Effects for {name}, in the game's retro pulp style: luminous, hand-inked comic energy "
        "effects with crisp contours and painterly glow, matching the effects in the attached "
        "style mockup. No character bodies, no hands, no props held by anyone.",
        "Rows:",
    ]
    for row_name, frames in zip(ROW_NAMES, rows):
        lines.append(f"  Row ({row_name}): frame 1: {frames[0]}; frame 2: {frames[1]}; "
                     f"frame 3: {frames[2]}.")
    lines += [
        "Every effect travels or faces screen-right.",
        "Backdrop: fully transparent background (a real alpha channel), no colour fill.",
        "Constraints: no text, no labels, no grid lines, no borders, no floor, no scenery, no "
        "watermark; every frame complete and never cut off by the canvas edge.",
    ]
    return "\n".join(lines)


def construct_prompt() -> str:
    lines = [
        "Use case: stylized-concept",
        "Asset type: 2D side-view game prop sprites (four placed ability constructs)",
        "Canvas: landscape 3:2. Four props in one row, evenly spaced with wide empty gaps, each "
        "standing on the same invisible ground line, side view, in the game's retro pulp style "
        "(hand-inked contours, engraved hatching, painted colour), matching the attached mockup:",
    ]
    for number, (key, text) in enumerate(CONSTRUCTS.items(), start=1):
        lines.append(f"  Prop {number} ({key.replace('_', ' ')}): {text}.")
    lines += [
        "Backdrop: fully transparent background (a real alpha channel), no colour fill.",
        "Constraints: no characters, no text, no labels, no floor, no shadows, no scenery, no "
        "watermark; every prop complete and never cut off.",
    ]
    return "\n".join(lines)
