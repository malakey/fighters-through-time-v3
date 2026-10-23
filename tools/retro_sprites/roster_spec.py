"""Retro v2 sprite pass (2026-10-18): what every character sheet must show.

One entry per roster character: an identity lock (the costume and props every
generation must match), how the signature prop is carried, and the key poses
of every one of the 30 contract animations. Shared animations (locomotion,
traversal, reactions) read the prop phrases; combat, abilities and victory are
authored per character from design-godot.md Section 5 and the Package 13 kit
handoffs (docs/handoffs/P13_W5/W7a/W7b.md).

Body art shows only what is attached to the body (a blade trail, wings, a
cast flare). The world effect of an ability is its separate VFX atlas.
"""

from __future__ import annotations

STYLE = (
    "Retro pulp adventure-comic game art matching the attached gameplay mockups: "
    "confident hand-inked black contour lines, fine engraved hatching, subtle halftone "
    "print texture, rich painted colour with warm highlights, crisp readable silhouette. "
    "Not pixel art, not 3D, not anime, not chibi. Realistic adult proportions "
    "(about seven heads tall)."
)

CONSTRAINTS = (
    "No text, no labels, no numbers, no grid lines, no panel borders, no floor, no ground "
    "plane, no cast shadows, no scenery, no watermark. Every figure is complete: head, "
    "hands, feet, weapon tips and attached effects all inside its own area and never cut "
    "off by the canvas edge. Figures never touch or overlap each other."
)

# ---------------------------------------------------------------------------
# Characters
# ---------------------------------------------------------------------------

CHARACTERS: dict[str, dict] = {
    "einstein": {
        "prop_short": "the chalk",
        "name": "Albert Einstein",
        "identity": (
            "Albert Einstein, an older man with a wild shock of white hair and a bushy white "
            "moustache, brown tweed jacket, cream knitted sweater vest, white shirt and brown "
            "tie, baggy brown trousers, scuffed brown shoes"
        ),
        "prop": "a stick of chalk glowing bright cyan, held in his right hand",
        "prop_low": "the glowing chalk held low at his side",
        "stance": (
            "a feisty, scrappy fighting stance: body turned side-on, knees bent, weight on the "
            "balls of his feet, his near hand holding the glowing chalk raised in front of him at "
            "shoulder height and pointed at the enemy like a duelling wand, his far fist raised "
            "at chin height"
        ),
        "run_arms": "both arms pump with the elbows bent, the glowing chalk gripped in his near hand",
        "arms_a": "his far arm swung forward and his near (chalk) arm swung back",
        "arms_b": "his near (chalk) arm swung forward and his far arm swung back",
        "arms_pass": "both arms swinging close past his body",
        "guard": "fists raised in a boxer's guard in front of his face, chalk clenched, body turned side-on, knees bent",
        "effect": "bright cyan chalk-light",
        "target_height": 244,
    },
    "joan": {
        "prop_short": "the sword",
        "name": "Joan of Arc",
        "identity": (
            "Joan of Arc, a young woman with a short dark bob, polished steel plate armour, "
            "royal-blue surcoat with gold fleurs-de-lis, red-brown sash and red skirt lining, "
            "brown boots"
        ),
        "prop": "a steel broadsword in her right hand",
        "prop_low": "the broadsword held low, point down and forward",
        "stance": (
            "a knight's ready stance: body turned side-on, knees bent, feet set apart, the "
            "broadsword gripped in both hands at waist height with the blade angled up and "
            "forward, its point aimed at the enemy's throat"
        ),
        "run_arms": (
            "her near hand carries the broadsword low and angled back with the point trailing "
            "behind her, in the same place in every frame; her far arm pumps"
        ),
        "arms_a": "her far arm swung forward, the sword trailing low behind her",
        "arms_b": "her far arm swung back, the sword trailing low behind her",
        "arms_pass": "her far arm swinging past her body, the sword trailing low behind her",
        "guard": "sword held vertically in front of her body in both hands, blade up, feet braced wide",
        "effect": "golden holy light",
        "target_height": 240,
    },
    "leonardo": {
        "prop_short": "the paintbrush",
        "name": "Leonardo da Vinci",
        "identity": (
            "Leonardo da Vinci as a man in his prime: long wavy brown hair and a full brown "
            "beard, burgundy-red Renaissance tunic with puffed sleeves, brown leather artisan "
            "apron and harness, leather satchel at the hip, brown boots"
        ),
        "prop": "a long artist's paintbrush tipped with glowing ochre-gold paint in his right hand, a large brass drafting compass hanging from his belt",
        "prop_low": "the paintbrush held low",
        "stance": (
            "a balanced duellist's stance: body turned side-on, knees bent, the long paintbrush "
            "held up in front of him in his near hand like a short sword with its glowing tip "
            "angled toward the enemy, his far hand raised open beside his chest"
        ),
        "run_arms": "both arms pump with the elbows bent, the paintbrush gripped in his near hand",
        "arms_a": "his far arm swung forward and his near (brush) arm swung back",
        "arms_b": "his near (brush) arm swung forward and his far arm swung back",
        "arms_pass": "both arms swinging close past his body",
        "guard": "a hinged wooden-and-brass drafting board unfolded in front of him as a shield",
        "effect": "glowing ochre-gold paint",
        "target_height": 244,
    },
    "lincoln": {
        "prop_short": "the split rail",
        "name": "Abraham Lincoln",
        "identity": (
            "Abraham Lincoln, very tall and lean, chin-curtain beard without a moustache, black "
            "stovepipe hat, long black frock coat, dark waistcoat, black bow tie, white shirt, "
            "black trousers and shoes"
        ),
        "prop": "a heavy split-rail fence log (a rough wooden rail as long as he is tall) carried in both hands",
        "prop_low": "the split rail held across his body at waist height",
        "stance": (
            "a frontier wrestler's ready crouch: body turned side-on, knees bent, the heavy split "
            "rail gripped in both hands and held angled across his body like a quarterstaff, its "
            "front end raised toward the enemy"
        ),
        "run_arms": (
            "both hands grip the split rail and carry it level across his chest; his arms and the "
            "rail stay in the same place in every frame"
        ),
        "arms_a": "the rail carried level across his chest",
        "arms_b": "the rail carried level across his chest",
        "arms_pass": "the rail carried level across his chest",
        "guard": "the split rail held vertically in front of his body in both hands, braced",
        "effect": "amber wood splinters and earthen dust",
        "target_height": 258,
    },
    "cleopatra": {
        "prop_short": "the scepter",
        "name": "Cleopatra",
        "identity": (
            "Cleopatra, a regal Ptolemaic queen with straight black hair in a blunt fringe with "
            "braids, a gold uraeus diadem, a broad turquoise-and-gold collar, a floor-length white "
            "pleated linen gown with a gold belt (regal and dignified, no leg slits, modest "
            "neckline), gold arm cuffs, gold sandals"
        ),
        "prop": "a golden asp-wrapped scepter (a staff with a coiled cobra) in her right hand",
        "prop_low": "the golden scepter held upright at her side",
        "stance": (
            "a regal fighting stance: body turned side-on, knees softly bent, the golden asp "
            "scepter raised forward in her near hand and pointed at the enemy, her far hand "
            "raised high behind her shoulder with the fingers spread like a striking cobra; her "
            "gown exactly as in Image 1, with its high, round neckline under the broad collar"
        ),
        "run_arms": (
            "her near hand carries the golden scepter upright at her side in the same place in "
            "every frame; her far arm swings; her gown streams behind her"
        ),
        "arms_a": "her far arm swung forward, the scepter upright at her side",
        "arms_b": "her far arm swung back, the scepter upright at her side",
        "arms_pass": "her far arm swinging past her body, the scepter upright at her side",
        "guard": "the scepter held diagonally across her body, a small swirl of golden sand in front of it",
        "effect": "swirling golden sand with teal-green serpent light",
        "target_height": 240,
    },
    "tesla": {
        "prop_short": "the copper cane",
        "name": "Nikola Tesla",
        "identity": (
            "Nikola Tesla, tall and slim, slicked black hair parted in the centre, thin black "
            "moustache, long black frock coat, burgundy waistcoat, white high-collared shirt, "
            "dark tie, black trousers and boots"
        ),
        "prop": "a copper-wound induction cane with a glass bulb at its tip, held in his right hand",
        "prop_low": "the copper induction cane held low, tip near the ground",
        "stance": (
            "a gentleman duellist's ready stance: body turned side-on, knees slightly bent, the "
            "copper induction cane held forward and low in his near hand like a sabre ready to "
            "thrust, its glass bulb glowing, his far hand raised open in front of his chest with "
            "a few small blue sparks between the fingertips"
        ),
        "run_arms": (
            "both arms pump with the elbows bent, the copper cane gripped in his near hand and "
            "angled back; his coat tails flutter behind him"
        ),
        "arms_a": "his far arm swung forward and his near (cane) arm swung back",
        "arms_b": "his near (cane) arm swung forward and his far arm swung back",
        "arms_pass": "both arms swinging close past his body",
        "guard": "the copper cane held horizontally in front of him in both hands, small blue sparks crackling along it",
        "effect": "blue-white electric arcs",
        "target_height": 250,
    },
    "shakespeare": {
        "prop_short": "the giant quill",
        "name": "William Shakespeare",
        "identity": (
            "William Shakespeare, high balding forehead, shoulder-length dark brown hair, thin "
            "moustache and goatee, a large white ruff collar, black-and-crimson slashed "
            "Elizabethan doublet, short padded trunk hose, dark hose and black shoes"
        ),
        "prop": "a giant white feather quill (as long as his arm) wielded like a blade in his right hand",
        "prop_low": "the giant quill held low like a rapier",
        "stance": (
            "a stage duellist's en garde: body turned side-on, knees bent, the giant quill held "
            "forward at eye height in his near hand like a rapier with its point at the enemy, "
            "his far hand raised behind his head with a theatrical curl of the wrist"
        ),
        "run_arms": (
            "his near hand holds the giant quill angled back and down behind him like a trailing "
            "rapier, in the same place in every frame; his far arm pumps"
        ),
        "arms_a": "his far arm swung forward, the quill trailing behind him",
        "arms_b": "his far arm swung back, the quill trailing behind him",
        "arms_pass": "his far arm swinging past his body, the quill trailing behind him",
        "guard": "the giant quill held up crossways in front of him, a glowing page of script hovering before it",
        "effect": "glowing blue-black cursive ink",
        "target_height": 244,
    },
    "mozart": {
        "prop_short": "the baton",
        "name": "Wolfgang Amadeus Mozart",
        "identity": (
            "young Wolfgang Amadeus Mozart, powdered white hair tied back with side curls, "
            "burgundy tailcoat with gold trim, cream embroidered waistcoat, white lace cravat, "
            "tan breeches, white stockings, black buckled shoes"
        ),
        "prop": "a slim white conductor's baton in his right hand (never a sword)",
        "prop_low": "the baton held low between two fingers",
        "stance": (
            "a conductor's fighting stance: body turned side-on, knees slightly bent, both hands "
            "raised at shoulder height as if about to cue an orchestra, the baton held upright in "
            "his near hand, chin up, poised"
        ),
        "run_arms": (
            "both arms pump with the elbows bent, the baton gripped in his near hand; his "
            "tailcoat flaps behind him"
        ),
        "arms_a": "his far arm swung forward and his near (baton) arm swung back",
        "arms_b": "his near (baton) arm swung forward and his far arm swung back",
        "arms_pass": "both arms swinging close past his body",
        "guard": "both hands raised, baton up, a glowing blue-gold five-line music staff hanging in front of him like a barrier",
        "effect": "glowing blue-and-gold musical notes",
        "target_height": 236,
    },
    "tubman": {
        "prop_short": "the lantern and cane",
        "name": "Harriet Tubman",
        "identity": (
            "Harriet Tubman in the 1850s, a small, sturdy, determined Black woman with a calm "
            "dignified expression, a dark headwrap, a long dark-blue dress, a brown wool jacket "
            "and a grey shawl, worn brown boots; realistic and respectful, never a caricature"
        ),
        "prop": "a small brass lantern glowing warm gold in her right hand and a plain wooden walking cane in her left hand",
        "prop_low": "the lantern held low at her side, cane planted",
        # "One hand ... the other" let the first run sheet swap the lantern's
        # hand and drop the cane in two frames, which flickers in the loop:
        # every run phrase names the near and the far hand.
        "stance": (
            "a steady, determined fighting stance: body turned side-on, knees bent, feet planted "
            "wide, the wooden cane raised off the ground and held out in front of her in her far "
            "hand like a fighting stick, its tip pointed at the enemy at chest height (it never "
            "touches the ground), the glowing brass lantern raised in her near hand at shoulder "
            "height"
        ),
        "run_arms": (
            "her near hand holds the glowing brass lantern out in front of her at waist height, "
            "steady, in every frame; her far hand grips the wooden cane, which swings with her "
            "stride; both are visible in every frame"
        ),
        "arms_a": "the cane swung forward, the lantern held out in front of her",
        "arms_b": "the cane swung back, the lantern held out in front of her",
        "arms_pass": "the cane swinging past her body, the lantern held out in front of her",
        "guard": "the cane held across her body in both hands, lantern hanging from her wrist, braced",
        "effect": "warm gold-white North Star lantern light",
        "target_height": 236,
    },
}

# ---------------------------------------------------------------------------
# Shared animations. {prop}, {prop_low}, {stance}, {arms_a}, {arms_b},
# {arms_pass}, {guard}, {effect} and {name} are filled from the character
# entry. Each value is a list of frame descriptions in order.
# ---------------------------------------------------------------------------

SHARED_POSES: dict[str, list[str]] = {
    # 2026-10-20 (user): the idle is a fighting stance with the hands up, not a
    # breathing stand. One stance bouncing gently in place; JOB_NOTES holds the
    # stance itself and the "only the knees and the body height move" rule.
    "idle": [
        "{stance}; the top of a small, springy bounce, the upper body at its highest",
        "the same stance sinking: the knees bend a little more and the upper body drops a few pixels",
        "the same stance at the bottom of the bounce: only slightly lower than the frame before, a small dip and never a squat",
        "the same stance rising: halfway back up to the first frame",
    ],
    # 2026-10-20 (user): the 6-frame run read as jerky and incomplete. A classic
    # 8-frame cycle, contact / down / passing / flight for each leg; near means
    # the side closer to the viewer. The arm phrases come from the character
    # entry, so a prop carried in both hands (Lincoln) never swings.
    "run": [
        "contact: the near leg reaching forward, its heel touching the ground ahead of the body; the far leg stretched back, toes just leaving the ground; body at middle height; {arms_a}",
        "down: the near foot planted flat under the hips with the knee bent to take the weight; the far leg folding up behind, its heel rising toward the seat; body at its lowest; {arms_a}",
        "passing: the near leg straightening under the body; the far knee swinging forward past it with the far foot tucked up high; body rising; {arms_pass}",
        "flight: both feet off the ground; the near leg pushed off and stretched out behind; the far knee driven high in front; body at its highest; {arms_b}",
        "contact: the far leg reaching forward, its heel touching the ground ahead of the body; the near leg stretched back, toes just leaving the ground; body at middle height; {arms_b}",
        "down: the far foot planted flat under the hips with the knee bent to take the weight; the near leg folding up behind, its heel rising toward the seat; body at its lowest; {arms_b}",
        "passing: the far leg straightening under the body; the near knee swinging forward past it with the near foot tucked up high; body rising; {arms_pass}",
        "flight: both feet off the ground; the far leg pushed off and stretched out behind; the near knee driven high in front; body at its highest; {arms_a}",
    ],
    "jump": [
        "launching upward: body stretched tall, toes just leaving the ground, arms thrown upward",
        "rising: knees drawing up, arms balancing, clothing trailing downward",
        "apex: knees tucked high, body compact, about to turn into a fall",
    ],
    "fall": [
        "falling: legs extended below, arms raised and spread for balance, hair and clothing blown upward",
        "falling: the same pose with limbs shifted slightly and clothing fluttering differently",
        "falling: the same pose, legs slightly bent, clothing fluttering upward",
    ],
    "skid": [
        "braking hard: front foot planted and sliding forward, body leaning back, a puff of dust at the front foot",
        "deeper brake: knees bent, leaning back against the momentum, both feet still on the ground (upright, never lying or kneeling), more dust",
        "recovering: straightening up and turning the hips to push off",
    ],
    "crouch": [
        "a low crouch: knees deeply bent, back straight, weight centred, {prop_low}",
        "the same low crouch, breathing in slightly",
        "the same low crouch, breathing out",
    ],
    "roll_startup": [
        "dipping into a forward roll: crouching forward, head tucking down",
        "reaching forward toward the ground with hands and shoulder, body curling",
        "a tight tucked ball tipping forward",
    ],
    "roll": [
        "a tight tucked ball rolling forward, upright",
        "inverted mid-roll: back on the ground, legs tucked over the head",
        "a tight tucked ball coming up onto the feet",
    ],
    "roll_recovery": [
        "crouched low coming out of the roll, one hand touching the ground",
        "rising from the crouch",
        "nearly standing, back in a ready stance",
    ],
    "ledge_hang": [
        "hanging from a ledge: both arms straight up with both hands gripping an invisible edge at the very top of the figure, body dangling below, legs hanging, {prop_short} stowed (slung across the back or tucked in the belt, never floating free)",
        "hanging the same way, body swaying slightly forward",
        "hanging the same way, body swaying slightly back",
    ],
    "ledge_pull_up": [
        "pulling up from a hang: elbows bent, chest raised to the hands, hands at the top of the figure",
        "torso hauled over the edge, one knee coming up, hands pushing down",
        "crouched on top, one knee up, about to stand",
    ],
    "ledge_drop": [
        "letting go of a ledge: arms still raised, body starting to drop",
        "dropping: arms up, legs straight below",
        "dropping: legs bending to land, arms coming down",
    ],
    "block": [
        "guard stance: {guard}",
        "the same guard stance, breathing in slightly",
        "the same guard stance, breathing out",
    ],
    "hitstun": [
        "struck: head snapped back, torso recoiling, arms thrown out",
        "staggering backward on the heels, off balance",
        "bent forward, wincing, recovering balance",
    ],
    "dazed": [
        "dizzy and dazed: wobbling stance, one hand to the forehead, small cartoon stars circling above the head, {prop_low}",
        "wobbling the other way, eyes unfocused, stars circling",
        "swaying upright, stars circling",
    ],
    "death": [
        "knocked out: knees buckling, body going limp",
        "falling to the ground on the side",
        "lying motionless flat on the ground, face down, {prop_short} fallen beside the body",
    ],
    "respawn": [
        "chronal rematerialisation: a translucent glowing cyan hologram silhouette of {name} standing in a ready stance",
        "half solid: the figure partly in normal colours with glowing cyan scanlines and sparks across the body",
        "fully solid in normal colours, a last few cyan sparks fading, ready stance",
    ],
    "defeat": [
        "defeated and exhausted: kneeling on one knee, head bowed, shoulders slumped, {prop_short} resting on the ground",
        "the same kneeling pose, heaving a heavy breath",
        "the same kneeling pose, head bowed lower",
    ],
}

# The run frames (0-based, in SHARED_POSES["run"] order) with both feet off the
# ground; every other frame of the cycle has a foot down (pack.py "cycle" feet).
RUN_FLIGHT_FRAMES = frozenset({3, 7})

# Per-character combat, ability and victory key poses (three frames each).
CHARACTER_POSES: dict[str, dict[str, list[str]]] = {
    "einstein": {
        "basic_attack_1": [
            "wind-up: stepping forward, chalk hand drawn back behind his head",
            "strike: slashing the chalk diagonally down in front of him, a bright cyan chalk-light arc sweeping through the air before him",
            "follow-through: chalk arm swept low across his body, the arc fading",
        ],
        "basic_attack_2": [
            "wind-up: chalk hand low and back by his hip",
            "strike: sweeping the chalk upward and forward, a rising cyan chalk-light arc in front of him",
            "follow-through: chalk arm high, body turning",
        ],
        "basic_attack_3": [
            "wind-up: rearing back, chalk pulled to his shoulder, cyan light gathering around it",
            "strike: thrusting the chalk forward at arm's length, a bright cyan kinetic shockwave ring bursting from its tip",
            "follow-through: leaning forward with the arm extended, the ring dissipating",
        ],
        "up_attack": [
            "wind-up: crouched low, chalk held down by his knee",
            "strike: springing up and slashing the chalk straight upward, a tall vertical cyan chalk-light arc above his head",
            "follow-through: on his toes with the chalk arm extended overhead",
        ],
        "down_air": [
            "airborne: knees tucked, chalk raised overhead",
            "airborne strike: slashing the chalk straight down beneath him, a vertical cyan chalk streak shooting down below his feet",
            "airborne: chalk arm pointing straight down, legs tucked",
        ],
        "grab": [
            "reaching forward with his open left hand at arm's length",
            "hand clenched at arm's length, grabbing hold of an unseen opponent",
            "pulling the clenched hand back toward his chest",
        ],
        "throw": [
            "gripping an unseen opponent at his hip with both hands, turning away",
            "heaving forward with both arms, a cyan chalk-light swoosh following the toss",
            "both arms extended forward after the release",
        ],
        "victory": [
            "tapping his temple with a knowing smile",
            "raising the glowing chalk triumphantly, cyan sparkles around it",
            "one finger raised in a eureka gesture, grinning",
        ],
        "special_1": [
            "winding up an underarm throw, a glowing cyan energy sphere the size of an apple in his right hand",
            "throwing forward, arm fully extended at shoulder height, the cyan energy sphere just leaving his fingertips",
            "follow-through: arm still extended, leaning forward",
        ],
        "special_2": [
            "drawing a glowing circle in the air with the chalk",
            "thrusting both palms forward, violet spacetime ripples spreading from his hands",
            "holding the pose, palms forward, faint violet ripples",
        ],
        "movement_ability": [
            "crouched, drawing a quick glowing cyan ring around himself with the chalk",
            "his body dissolving forward into translucent cyan-and-violet spacetime ribbons",
            "re-forming from the ribbons into a ready stance",
        ],
        "ultimate": [
            "scribbling a glowing chalk equation in the air, cyan symbols floating around him",
            "flicking a small black-and-violet singularity orb forward from his fingertips at arm's length",
            "arm extended, coat and hair whipped forward by the pull of the singularity",
        ],
    },
    "joan": {
        "basic_attack_1": [
            "wind-up: weight on the back foot, sword drawn back over her right shoulder",
            "strike: lunging forward, sword arm fully extended, blade horizontal at chest height, a golden light trail behind the blade",
            "follow-through: blade swept past and low in front, torso rotated",
        ],
        "basic_attack_2": [
            "wind-up: sword held low behind her left hip for a backhand cut",
            "strike: a sweeping backhand slash forward and across, a brighter golden trail following the blade",
            "follow-through: blade high on her right side after the sweep",
        ],
        "basic_attack_3": [
            "wind-up: sword raised high overhead in both hands, rising onto her toes",
            "strike: cleaving straight down in front of her, a blazing golden arc from overhead to the ground",
            "follow-through: blade low near the ground in front, golden sparks bursting where it struck",
        ],
        "up_attack": [
            "wind-up: crouched low with the sword held low behind her",
            "strike: slashing straight upward, a tall golden crescent of light above her head",
            "follow-through: standing tall with the sword pointing straight up",
        ],
        "down_air": [
            "airborne: knees tucked, sword raised overhead in both hands",
            "airborne strike: driving the sword straight down below her feet, a golden streak spiking downward",
            "airborne: sword pointing straight down, legs tucked",
        ],
        "grab": [
            "reaching forward with her open left gauntlet, sword held back in her right hand",
            "gauntlet clenched at arm's length, gripping an unseen opponent",
            "pulling the clenched gauntlet back to her hip",
        ],
        "throw": [
            "gripping an unseen opponent with both hands at her right hip, turning away",
            "heaving forward and up in a shoulder toss, surcoat swirling",
            "arms extended forward after the release",
        ],
        "victory": [
            "raising the sword overhead",
            "the sword held high with a radiant golden glint at its tip",
            "bringing the sword down to a salute in front of her face",
        ],
        "special_1": [
            "sword raised overhead, golden holy light gathering along the blade",
            "smiting down so the blade strikes the ground in front of her, a burst of golden light erupting at the impact",
            "crouched with the blade on the ground, golden light rolling forward along the ground",
        ],
        "special_2": [
            "sword drawn back at her hip for a thrust, golden light on the point",
            "lunging forward in a long thrust, several golden after-image thrusts flashing around the blade",
            "full lunge, sword point forward, golden sparks at the tip",
        ],
        "movement_ability": [
            "crouched, about to leap, sword low",
            "leaping upward in a sweeping upward slash, golden ethereal flaming wings unfurling from her back",
            "airborne at the apex with burning golden wings spread wide, sword held forward",
        ],
        "ultimate": [
            "raising the Banner of Orleans high overhead (a white-and-gold banner with fleurs-de-lis on a tall pole)",
            "planting the banner pole in the ground beside her, golden light bursting from its base",
            "standing beside the planted banner with her sword raised, commanding a charge",
        ],
    },
    "leonardo": {
        "basic_attack_1": [
            "wind-up: paintbrush drawn back over his shoulder",
            "strike: sweeping the brush forward, an arcing trail of glowing ochre-gold paint hanging in the air in front of him",
            "follow-through: brush swept low across his body",
        ],
        "basic_attack_2": [
            "wind-up: brush held low behind his hip",
            "strike: a reverse upward sweep leaving a deep blue-black ink arc in the air",
            "follow-through: brush high, body turned",
        ],
        "basic_attack_3": [
            "wind-up: the large brass drafting compass raised overhead in both hands",
            "strike: slamming the compass point-first down in front of him, a burst of golden geometric sparks",
            "follow-through: crouched over the compass planted in the ground",
        ],
        "up_attack": [
            "wind-up: crouched, brush held low",
            "strike: flicking the brush straight up, a rising arc of ochre-gold paint overhead",
            "follow-through: brush held high",
        ],
        "down_air": [
            "airborne: knees tucked, brass compass raised overhead",
            "airborne strike: stabbing the compass straight down below his feet, gold sparks trailing downward",
            "airborne: compass pointing straight down, legs tucked",
        ],
        "grab": [
            "reaching forward with his open left hand",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to his chest",
        ],
        "throw": [
            "gripping an unseen opponent at his hip with both hands, turning away",
            "heaving forward with both arms, a swirl of ochre paint following the toss",
            "both arms extended forward after the release",
        ],
        "victory": [
            "studying an invisible canvas with the brush raised to his eye",
            "flourishing the brush, golden paint sparks spiralling",
            "a satisfied bow with the brush held to his chest",
        ],
        "special_1": [
            "drawing a glowing golden spiral in the air with the brush",
            "arms spread wide as a glowing golden Fibonacci spiral expands around him",
            "brush held up, the spiral light fading",
        ],
        "special_2": [
            "pulling a small folded brass-and-wood clockwork gadget out of his satchel",
            "kneeling and setting the gadget down on the ground in front of his feet, brass sparks and tiny gears flying",
            "rising, a small brass winding key in his hand",
        ],
        "movement_ability": [
            "crouched, mechanical wooden-and-canvas ornithopter wings unfolding from his back harness",
            "boosting upward with the ornithopter wings beating down",
            "gliding with the ornithopter wings spread wide, body leaning forward",
        ],
        "ultimate": [
            "sketching a glowing golden circle-and-square Vitruvian diagram in the air with the brush",
            "thrusting both hands forward, launching a glowing golden geometric sphere",
            "arms spread wide, blueprint-blue light lines around him",
        ],
    },
    "lincoln": {
        "basic_attack_1": [
            "wind-up: the rail drawn back over his right shoulder",
            "strike: swinging the rail horizontally in a wide arc at waist height, amber wood splinters trailing",
            "follow-through: rail carried through past his left side",
        ],
        "basic_attack_2": [
            "wind-up: rail held low behind him",
            "strike: swinging the rail up in a heavy rising vertical arc in front of him",
            "follow-through: rail held high overhead",
        ],
        "basic_attack_3": [
            "wind-up: rail raised high overhead in both hands",
            "strike: crushing the rail straight down onto the ground in front of him, splinters and dust bursting",
            "follow-through: hunched over the rail lying on the ground",
        ],
        "up_attack": [
            "wind-up: crouched, rail held low",
            "strike: jabbing the rail straight upward overhead with both hands",
            "follow-through: rail held vertically overhead",
        ],
        "down_air": [
            "airborne: knees tucked, rail raised overhead",
            "airborne strike: driving the rail straight down below his feet, splinters spraying",
            "airborne: rail pointing straight down, legs tucked",
        ],
        "grab": [
            "reaching forward with his long open left hand, rail held in his right",
            "long hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to his chest",
        ],
        "throw": [
            "gripping an unseen opponent with both hands, lifting",
            "heaving the unseen opponent overhead and forward",
            "both arms extended forward after the release",
        ],
        "victory": [
            "tipping his stovepipe hat",
            "holding the stovepipe hat aloft in triumph",
            "resting one hand on the split rail planted upright beside him",
        ],
        "special_1": [
            "rail raised overhead",
            "slamming the rail end into the ground in front, an earthen golden shockwave bursting forward along the ground",
            "crouched, the rail planted in the ground",
        ],
        "special_2": [
            "rearing up with the rail raised high overhead",
            "bringing the rail down in a huge overhead arc in front of him, a golden-amber arc trail",
            "rail driven down to the ground, splinters flying",
        ],
        "movement_ability": [
            "tucking the rail under his arm like a battering ram, leaning forward",
            "charging forward with the rail levelled ahead, cyan and amber speed lines streaming behind him",
            "mid-charge stride, coat tails flying",
        ],
        "ultimate": [
            "raising the rail high overhead",
            "slamming the rail into the ground, split-rail fence posts erupting from the ground in front of him",
            "leaping with the rail raised for a final smash",
        ],
    },
    "cleopatra": {
        "basic_attack_1": [
            "wind-up: scepter drawn back",
            "strike: sweeping the scepter forward, releasing a quick wave of golden sand",
            "follow-through: scepter swept low across her body",
        ],
        "basic_attack_2": [
            "wind-up: scepter held low behind her",
            "strike: a reverse rising sweep with a fan of golden sand",
            "follow-through: scepter high",
        ],
        "basic_attack_3": [
            "wind-up: scepter pulled back to her shoulder, the cobra head glowing teal",
            "strike: thrusting the scepter forward, a blast of golden sand with a teal-green venom glint bursting from the cobra head",
            "follow-through: leaning into the thrust",
        ],
        "up_attack": [
            "wind-up: crouched, scepter low",
            "strike: swinging the scepter straight upward, sand spiralling overhead",
            "follow-through: scepter held high",
        ],
        "down_air": [
            "airborne: knees tucked, scepter raised",
            "airborne strike: stabbing the scepter straight down below her feet, a jet of golden sand beneath",
            "airborne: scepter pointing straight down",
        ],
        "grab": [
            "reaching forward with her open left hand, gold cuffs gleaming",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to her side",
        ],
        "throw": [
            "gripping an unseen opponent with both hands, turning",
            "flinging forward, golden sand swirling with the toss",
            "arms extended forward after the release",
        ],
        "victory": [
            "a regal pose, scepter raised",
            "scepter held high, golden sand swirling around her",
            "arms folded, scepter in the crook of her arm, a confident smile",
        ],
        "special_1": [
            "pointing the scepter at the ground in front of her",
            "commanding gesture, a swirl of sand and a few glowing teal spectral serpent heads rising at her feet",
            "scepter raised, the sand settling",
        ],
        "special_2": [
            "swirling the scepter overhead, golden sand gathering",
            "casting forward, a spiral of sand leaving the scepter",
            "scepter held forward, sand streaming",
        ],
        "movement_ability": [
            "crouching, her body beginning to dissolve into streaming golden sand grains",
            "a streaking cloud of golden sand in her shape rushing forward",
            "re-forming from the sand into a stance",
        ],
        "ultimate": [
            "raising the scepter high, sand swirling around her",
            "thrusting the scepter forward as a giant glowing teal spectral cobra lunges out of it",
            "a regal pose amid swirling sand",
        ],
    },
    "tesla": {
        "basic_attack_1": [
            "wind-up: cane drawn back",
            "strike: whipping the cane forward, quick blue-white sparks crackling off the tip",
            "follow-through: cane swept low",
        ],
        "basic_attack_2": [
            "wind-up: cane held low behind him",
            "strike: a reverse rising strike with a crackling blue-white arc",
            "follow-through: cane high",
        ],
        "basic_attack_3": [
            "wind-up: cane pulled back to his shoulder, the bulb glowing",
            "strike: thrusting the cane forward, a crackling blue-white electric discharge bursting from the bulb",
            "follow-through: leaning into the thrust",
        ],
        "up_attack": [
            "wind-up: crouched, cane low",
            "strike: swinging the cane straight upward, a lightning arc crackling overhead",
            "follow-through: cane held high",
        ],
        "down_air": [
            "airborne: knees tucked, cane raised",
            "airborne strike: thrusting the cane straight down below his feet, a lightning bolt striking downward",
            "airborne: cane pointing straight down",
        ],
        "grab": [
            "reaching forward with his open left hand, small sparks on his fingertips",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to his chest",
        ],
        "throw": [
            "gripping an unseen opponent at his hip, turning",
            "heaving forward with both arms, a blue-white arc following the toss",
            "arms extended forward after the release",
        ],
        "victory": [
            "adjusting his cuffs with a cool smile",
            "cane raised, crackling with electricity",
            "standing tall, cane planted, sparks dancing along it",
        ],
        "special_1": [
            "producing a compact copper Tesla coil from his coat",
            "setting the coil down on the ground in front of him, sparks leaping",
            "stepping back with the cane raised",
        ],
        "special_2": [
            "raising the cane overhead, charging it with blue-white light",
            "slamming the cane down as a ring of blue electromagnetic force bursts outward around him",
            "cane planted, field lines fading",
        ],
        "movement_ability": [
            "crouching as blue-white electricity engulfs his body",
            "his body a glowing blue-white silhouette of pure lightning streaking forward",
            "re-forming in a stance with residual sparks",
        ],
        "ultimate": [
            "raising the cane high, lightning arcing up from it",
            "pointing the cane forward and firing a straight blue-white teleforce beam from its tip",
            "bracing, sparks crackling around him",
        ],
    },
    "shakespeare": {
        "basic_attack_1": [
            "wind-up: quill drawn back over his shoulder",
            "strike: a diagonal quill slash leaving a glowing blue-black trail of cursive ink in the air",
            "follow-through: quill swept low",
        ],
        "basic_attack_2": [
            "wind-up: quill held low behind his hip",
            "strike: a horizontal sweep painting a glowing ink line through the air",
            "follow-through: quill high, body turned",
        ],
        "basic_attack_3": [
            "wind-up: quill drawn back like a fencer",
            "strike: thrusting the quill forward in a punctuation strike, a burst of glowing ink at its tip",
            "follow-through: leaning into the thrust",
        ],
        "up_attack": [
            "wind-up: crouched, quill low",
            "strike: a rising quill flourish, a curl of glowing ink overhead",
            "follow-through: quill held high",
        ],
        "down_air": [
            "airborne: knees tucked, quill raised",
            "airborne strike: stabbing the quill straight down below his feet, a jet of glowing ink beneath",
            "airborne: quill pointing straight down",
        ],
        "grab": [
            "reaching forward with his open left hand, theatrical",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to his chest",
        ],
        "throw": [
            "gripping an unseen opponent, turning with a flourish",
            "heaving forward, a swirl of ink following the toss",
            "arms extended forward after the release",
        ],
        "victory": [
            "a theatrical bow with a sweep of the quill",
            "quill raised dramatically, ink sparkles",
            "hand on his heart, chin raised proudly",
        ],
        "special_1": [
            "holding up a pale skull in his left hand, contemplating it",
            "hurling the skull forward, ghostly blue wisps trailing from it",
            "follow-through, arm extended",
        ],
        "special_2": [
            "sweeping the quill in a circle",
            "thrusting the quill forward, a swirling blue-grey gust of wind and ink spiralling in front of him",
            "holding the pose, his clothes blown by the gust",
        ],
        "movement_ability": [
            "crouched, a magical swirling gust gathering around his feet",
            "propelled up and forward on a swirling blue gust, ruff and doublet flapping",
            "airborne, gliding forward on the gust",
        ],
        "ultimate": [
            "raising the quill like a conductor in a blue theatrical spotlight glow",
            "slashing a long straight stroke of glowing ink forward",
            "a dramatic pose, arm outflung",
        ],
    },
    "mozart": {
        "basic_attack_1": [
            "wind-up: baton drawn back",
            "strike: flicking the baton forward, firing a small glowing blue-gold treble-clef pulse",
            "follow-through: baton swept low",
        ],
        "basic_attack_2": [
            "wind-up: baton held low",
            "strike: a reverse upward flick, a spray of glowing blue-and-gold notes",
            "follow-through: baton high",
        ],
        "basic_attack_3": [
            "wind-up: both arms raised like conducting a crescendo",
            "strike: a sweeping downbeat, a burst of glowing musical notes shoving forward",
            "follow-through: bowed forward, baton low",
        ],
        "up_attack": [
            "wind-up: crouched, baton low",
            "strike: sweeping the baton straight up, a rising swirl of glowing notes",
            "follow-through: baton held high",
        ],
        "down_air": [
            "airborne: knees tucked, baton raised",
            "airborne strike: jabbing the baton straight down below his feet, glowing notes streaming downward",
            "airborne: baton pointing straight down",
        ],
        "grab": [
            "reaching forward with his open left hand",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to his chest",
        ],
        "throw": [
            "gripping an unseen opponent, turning",
            "flinging forward with a flourish, glowing notes swirling",
            "arms extended forward after the release",
        ],
        "victory": [
            "conducting an invisible orchestra with a flourish",
            "a final dramatic cutoff, baton raised high",
            "a courtly bow, baton held to his chest",
        ],
        "special_1": [
            "baton raised, glowing notes gathering at its tip",
            "thrusting the baton forward, firing a cluster of glowing chord notes",
            "follow-through, baton extended",
        ],
        "special_2": [
            "both arms raised high, conducting a crescendo",
            "sweeping both arms forward, a wall of glowing blue-gold sound waves surging forward in front of him",
            "holding the pose, arms forward",
        ],
        "movement_ability": [
            "crouched, glowing notes swirling around his feet",
            "rising upward on a sweep of glowing blue-gold notes",
            "floating, standing on a short glowing five-line music staff",
        ],
        "ultimate": [
            "hovering slightly, arms raised to conduct",
            "a dramatic downbeat as glowing piano keys fall around him",
            "a grand finale pose, arms flung wide",
        ],
    },
    "tubman": {
        "basic_attack_1": [
            "wind-up: cane drawn back",
            "strike: a quick forward cane strike at chest height",
            "follow-through: cane swept low",
        ],
        "basic_attack_2": [
            "wind-up: cane held low behind her",
            "strike: a quick backhand cane strike",
            "follow-through: cane high",
        ],
        "basic_attack_3": [
            "wind-up: lantern drawn back low",
            "strike: swinging the lantern forward and up in an arc, a bright flare of gold-white North Star light",
            "follow-through: lantern raised high, light fading",
        ],
        "up_attack": [
            "wind-up: crouched, cane low",
            "strike: sweeping the cane straight upward, an arc of gold starlight overhead",
            "follow-through: cane held high",
        ],
        "down_air": [
            "airborne: knees tucked, cane raised in both hands",
            "airborne strike: driving the cane straight down below her feet, gold starlight trailing down",
            "airborne: cane pointing straight down",
        ],
        "grab": [
            "reaching forward with her open hand, lantern held back",
            "hand clenched at arm's length, gripping an unseen opponent",
            "pulling the hand back to her side",
        ],
        "throw": [
            "gripping an unseen opponent, turning her hips",
            "heaving forward, lantern light streaking",
            "arms extended forward after the release",
        ],
        "victory": [
            "raising the lantern high, it shines brightly",
            "the lantern held high, a calm determined look toward the horizon",
            "lowering the lantern, a quiet nod",
        ],
        "special_1": [
            "raising the lantern and calling out",
            "sweeping the lantern forward as pale-blue spectral scouts surge forward along the ground beside her",
            "lantern held forward, the spectral light streaming ahead",
        ],
        "special_2": [
            "a calm watchful counter stance, lantern held low and still, a faint gold star glint in front of her",
            "snapping into a rising lantern strike, a bright gold flare",
            "follow-through, lantern raised",
        ],
        "movement_ability": [
            "crouching, looking up toward a bright guiding star",
            "leaping forward and up, a trail of gold starlight behind her",
            "airborne, lantern held forward",
        ],
        "ultimate": [
            "raising the lantern high as it blazes",
            "thrusting the lantern forward, a broad beam of warm gold-white light pouring from it",
            "standing firm, the lantern still shining",
        ],
    },
}

# ---------------------------------------------------------------------------
# Generation jobs: which animations share one image and how it is laid out.
#   layout "row":  one row; "rows": two rows; "grid": 2x2 quadrants.
#   anchor: the row opens with a scale-reference figure (discarded).
#   feet:   "ground" keeps the row's shared ground line (grounded moves);
#           "lowest" anchors each frame's lowest solid row (airborne moves);
#           "cycle" stands every frame with a foot down on its own soles
#           and keeps a flight frame's drawn lift above the row's floor
#           (RUN_FLIGHT_FRAMES), never below it.
# ---------------------------------------------------------------------------

AIRBORNE_MOVEMENT = {"joan", "leonardo", "shakespeare", "mozart", "tubman"}

JOBS: list[dict] = [
    # The idle stance is crouched, so it cannot be its own scale reference:
    # the row opens with the standing figure every other sheet is scaled by.
    # Each stance frame stands on its own soles ("lowest"): the reference's
    # feet sit a few pixels off the stance's, which floated the idle.
    {"job": "idle", "layout": "row", "rows": [{"anchor": True, "anims": [("idle", 4)]}],
     "feet": {"idle": "lowest"}},
    # Two rows of anchor + four. "cycle" feet: see feet modes above.
    {"job": "run", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("run", 4)]},
        {"anchor": True, "anims": [("run", 4)]}], "feet": {"run": "cycle"}},
    {"job": "jump_fall", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("jump", 3)]}, {"anchor": True, "anims": [("fall", 3)]}],
     "feet": {"jump": "lowest", "fall": "lowest"}},
    {"job": "skid_crouch", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("skid", 3)]}, {"anchor": True, "anims": [("crouch", 3)]}],
     "feet": {"skid": "ground", "crouch": "ground"}},
    {"job": "roll_a", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("roll_startup", 3)]}, {"anchor": True, "anims": [("roll", 3)]}],
     "feet": {"roll_startup": "ground", "roll": "ground"}},
    {"job": "roll_b", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("roll_recovery", 3)]}, {"anchor": True, "anims": [("ledge_drop", 3)]}],
     "feet": {"roll_recovery": "ground", "ledge_drop": "lowest"}},
    {"job": "ledge", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("ledge_hang", 3)]}, {"anchor": True, "anims": [("ledge_pull_up", 3)]}],
     "feet": {"ledge_hang": "lowest", "ledge_pull_up": "lowest"}},
    {"job": "block_hitstun", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("block", 3)]}, {"anchor": True, "anims": [("hitstun", 3)]}],
     "feet": {"block": "ground", "hitstun": "ground"}},
    {"job": "dazed_defeat", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("dazed", 3)]}, {"anchor": True, "anims": [("defeat", 3)]}],
     "feet": {"dazed": "ground", "defeat": "ground"}},
    {"job": "respawn_victory", "layout": "rows", "rows": [
        {"anchor": True, "anims": [("respawn", 3)]}, {"anchor": True, "anims": [("victory", 3)]}],
     "feet": {"respawn": "ground", "victory": "ground"}},
] + [
    {"job": name, "layout": "grid", "rows": [
        {"anchor": True, "anims": [(name, 1)]}, {"anchor": False, "anims": [(name, 2)]}],
     "feet": {name: feet}}
    for name, feet in (
        ("death", "ground"),
        ("basic_attack_1", "ground"), ("basic_attack_2", "ground"), ("basic_attack_3", "ground"),
        ("up_attack", "ground"), ("down_air", "lowest"),
        ("grab", "ground"), ("throw", "ground"),
        ("special_1", "ground"), ("special_2", "ground"),
        ("movement_ability", "per_character"), ("ultimate", "ground"),
    )
]


# Loop-wide instructions printed once under a job's figure list: the frames
# of a loop must agree with each other, which per-figure lines cannot say.
JOB_NOTES: dict[str, str] = {
    "idle": (
        "The four idle frames are one fighting stance bouncing gently in place, like a 2D "
        "fighting-game idle loop: {stance}. It is a ready-to-attack stance, not a defensive "
        "block. The feet stay planted on exactly the same spots, and the head, both arms, the "
        "hands and {prop_short} keep exactly the same pose and angle in every frame; only the "
        "knee bend and the height of the upper body change. The bounce is small: from frame 1 "
        "to frame 3 the head drops by no more than one twentieth of the character's height, "
        "and frame 4 flows straight back into frame 1."
    ),
    "run": (
        "The eight run frames are one complete, seamless looping run cycle, like a professional "
        "2D fighting-game sprite sheet: frame 8 flows straight back into frame 1, and frames 5-8 "
        "repeat frames 1-4 with the near and far limbs swapped. The character runs in place: the "
        "torso keeps the same forward lean over the same spot in every frame, the head keeps the "
        "same size and angle, and the body bobs only a little (lowest in the down frames, highest "
        "in the flight frames). Arms: {run_arms}. Near means the side closer to the viewer, far "
        "the side partly hidden behind the body."
    ),
}


def job_notes(character: str, job_name: str) -> str:
    note = JOB_NOTES.get(job_name)
    return note.format(**CHARACTERS[character]) if note else ""


def feet_mode(job: dict, anim: str, character: str) -> str:
    mode = job["feet"].get(anim, "ground")
    if mode == "per_character":
        return "lowest" if character in AIRBORNE_MOVEMENT else "ground"
    return mode


def poses(character: str, anim: str) -> list[str]:
    entry = CHARACTERS[character]
    if anim in CHARACTER_POSES[character]:
        frames = CHARACTER_POSES[character][anim]
    else:
        frames = SHARED_POSES[anim]
    return [f.format(**entry) for f in frames]
