"""Generates resources/UI/ftt_theme.tres and scripts/UI/UIPalette.Roles.cs.

TG-THEME (Temporal Glass rebuild, 2026-10-27; first written by UR-THEME for the pulp
redesign). Run from the repository root:

    py tools/ui_theme/build_ftt_theme.py

The theme is a large, regular text resource; generating it keeps every value in
one table: the palette (UI_SPEC_GLASS section 1), the Label roles (section 2.3,
T01-T60, with the scene halos of 2.4) and the styles (sections 3.0-3.3 and 6.2).
The output is committed; edit this script, not the .tres.

Step 1 of TG-THEME builds every glass style in the 6.2 StyleBoxFlat fallback form
(bevels by corner_detail = 1, parallelograms by skew), so screens stay usable before
TG-ART's nine-slices land; step 2 wires the nine-slices. The pulp-only variations
(paper, banners, boss plates, slot plates, cards, bubbles, stamp) keep their pulp
plates until the closeout retires them.

The palette lives in one table (PALETTE, plan ruling R11): every colour a style or
role uses is looked up there by its UIPalette name, and the table is written into
UIPalette.Roles.cs (GeneratorPalette) so UIThemeTests can pin each entry to the
UIPalette constant of the same name. A PAL-A swap is a change to this table.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


# ---- palette (PAL-B "Resonance Gold" in the glass system) -------------------
# name (= the UIPalette constant) -> (hex, alpha). Section 1.1 tokens, 1.3 literals and 1.4 build fills.
PALETTE = {}
_PALETTE_DOC = {}


def pal(name, hexstr, alpha=1.0, doc=""):
    PALETTE[name] = (hexstr.lstrip("#").upper(), alpha)
    _PALETTE_DOC[name] = doc


# tokens
pal("Backdrop", "05071A", doc="bg0: the dark field under art, the screen dissolve")
pal("Ink", "F3F0E6", doc="primary text")
pal("Ink2", "C0BAA6", doc="secondary text; the normal chevron label")
pal("Ink3", "8A8374", doc="disabled text and dimmed non-text")
pal("InkInv", "1A1204", doc="text on a gold fill")
pal("Accent", "F3C95B", doc="gold accent")
pal("Accent2", "FFE39A", doc="light gold")
pal("Focus", "FFD45C", doc="the focused edge and marker")
pal("Edge", "F3C95B", 0.55, doc="the standard hairline")
pal("EdgeHi", "FFD45C", doc="the lit hairline")
pal("EdgeDim", "F3C95B", 0.25, doc="the quiet hairline")
pal("Res", "F3C95B", doc="Resonance gold")
pal("Res2", "FFF0B0", doc="Resonance bright")
pal("Hp", "8CF0B8", doc="HP fill, moving end")
pal("Hp2", "4FCF8A", doc="HP fill, anchored end")
pal("HpLag", "FF5A5A", doc="damage tracker (drawn at 0.9)")
pal("Echo", "FFF7DA", doc="Rally echo band")
pal("Meter", "F3C95B", doc="Ultimate meter start")
pal("Meter2", "FFB347", doc="Ultimate meter end")
pal("Shield", "DFE8FF", doc="block pip")
pal("Danger", "FF5A5A", doc="danger, low HP")
pal("Boss", "FF5A5A", doc="boss red")
pal("Boss2", "A82A2A", doc="boss fill start, low-HP fill start")
pal("StatusDamage", "FF7A5C", doc="damage status glyph and ring")
pal("StatusControl", "8FC7FF", doc="control status glyph and ring")
pal("PanelShadow", "00020C", 0.60, doc="the soft drop shadow")
# glass literals (1.3) and build fills (1.4)
pal("GlassFillTop", "0F1834", 0.92, doc="panel fill, 160 degree start (build)")
pal("GlassFillBottom", "060A1A", 0.95, doc="panel fill, 160 degree end (build)")
pal("GlassFillFlat", "0F1834", 0.93, doc="the 6.2 single-colour fallback of the panel fill")
pal("ButtonFill", "080D20", 0.90, doc="chevron and field fill (normal, selected)")
pal("ButtonFillFocus", "081026", 0.92, doc="chevron fill under the focus and pressed washes")
pal("ButtonFillPressed", "5A5138", 0.946, doc="the pressed wash (mean .33) composited over ButtonFillFocus")
pal("ButtonFillDisabled", "080D20", 0.50, doc="disabled chevron fill")
pal("TileFillTop", "1A264A", 0.90, doc="select tile fill, 160 degree start")
pal("TileFillBottom", "060A1A", 0.94, doc="select tile fill, 160 degree end")
pal("LensFill", "060A1A", 0.92, doc="lens radial outer stop")
pal("BadgeFill", "080C1E", 0.86, doc="badge radial outer stop")
pal("DialFill", "050918", 0.90, doc="Integrity dial radial outer stop")
pal("PortraitFill", "060A1A", 0.95, doc="portrait radial outer stop")
pal("TubeTrackTop", "02050E", 0.95, doc="glass tube inner track, top")
pal("TubeTrackBottom", "0A1024", 0.82, doc="glass tube inner track, bottom")
pal("BossTrackTop", "05060F", doc="boss tube track, top (opaque)")
pal("BossTrackBottom", "120A18", doc="boss tube track, bottom (opaque)")
pal("SweepShade", "02050E", 0.76, doc="cooldown sweep")
pal("WedgeRemainder", "02050E", 0.55, doc="Integrity wedge remainder")
pal("ToggleTrackFill", "060A1A", 0.90, doc="toggle track fill (off)")
pal("TabPanelFill", "060A1A", 0.60, doc="the TabContainer's quiet inner panel (agent choice)")
pal("SliderTrack", "FFFFFF", 0.12, doc="slider track")
pal("ScrollTrack", "FFFFFF", 0.06, doc="scroll bar track")
pal("TickLine", "FFFFFF", 0.13, doc="tube decile ticks")
pal("RingTrack", "F3C95B", 0.16, doc="readiness and status ring remainder; tube glow")
pal("FocusGlow", "F3C95B", 0.55, doc="the 14 px focus glow")
pal("FocusWash", "F3C95B", 0.22, doc="a popup row's focus wash (flat mean of the button wash)")
pal("TagWash", "F3C95B", 0.21, doc="the panel tag's wash (flat mean of .34 to .08)")
pal("TabFill", "F3C95B", 0.08, doc="an unselected tab")
pal("TabHover", "F3C95B", 0.16, doc="a hovered tab (agent choice)")
pal("ReadyBadgeFill", "F3C95B", 0.14, doc="the READY badge fill")
pal("ReadyBadgeGlow", "F3C95B", 0.30, doc="the READY badge glow")
pal("ReadyBadgeIdleEdge", "F3C95B", 0.25, doc="the idle badge border")
pal("ReadyBadgeIdleFill", "000000", 0.25, doc="the idle badge fill")
pal("Selection", "F3C95B", 0.40, doc="text selection in a field")
pal("PipGlow", "8FD8FF", 0.60, doc="block pip glow")
pal("DefyReadyGlow", "FFE87A", 0.75, doc="Defy-ready glow")
pal("BossEdge", "FF4D62", doc="boss tube edge (PAL-A's boss red, hard-coded by the mock)")
pal("BossGlow", "FF4D62", 0.30, doc="boss tube glow")
pal("BossTracker", "FFFFFF", 0.55, doc="boss damage tracker")
pal("TabSelectedInk", "04141A", doc="selected tab text; the select lock glyph")
pal("ScrimFill", "03050E", doc="what ScrimPanel paints; every user sets SelfModulate alpha 0.70")
pal("PauseDim", "03050E", 0.70, doc="the build pause dim (ScrimFill at 0.70)")
pal("GameOverDim", "03050E", 0.82, doc="the Smothered dim")
pal("ScrimMenu", "040714", 0.96, doc="the menu scrim's strongest stop (90 degree: .96, .84 at 26 %, .35 at 48 %, 0 at 66 %)")
pal("SelectVignette", "030510", 0.55, doc="the select vignette's centre stop")
pal("TopShade", "06040C", 0.80, doc="the Fighter top shade's top stop (.80, .64 at 42 %, .30 at 78 %, 0)")
pal("TitleTop", "FFFFFF", doc="menu title gradient, top")
pal("TitleMid", "DFE8FF", doc="menu title gradient at 42 %; the flat fallback")
pal("TitleBottom", "8D9BC6", doc="menu title gradient, bottom")
pal("TextFocus", "FFFFFF", doc="the focused, hovered and pressed label")
pal("HaloOutlineS", "000000", 0.85, doc="halo S rim")
pal("HaloShadowS", "000000", 0.60, doc="halo S shadow")
pal("HaloOutlineL", "000000", 0.70, doc="halo L rim")
pal("HaloShadowL", "000000", 0.50, doc="halo L shadow")
# legacy (pulp) colours the retired roles still use until the closeout
pal("LabelDark", "0C0A10", doc="legacy pulp: dark text on gold or paper")


def col(hexstr, a=1.0):
    hexstr = hexstr.lstrip("#")
    r, g, b = (int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return "Color(%.7g, %.7g, %.7g, %.7g)" % (r, g, b, a)


def C(name):
    """A palette entry as a Godot Color literal. Every style colour goes through the table."""
    hexstr, alpha = PALETTE[name]
    return col(hexstr, alpha)


# ---- resource bookkeeping --------------------------------------------------
ext = []      # (type, path, id)
subs = []     # (type, id, [(k, v)])
_dedupe = {}


def add_ext(typ, path, ident):
    ext.append((typ, path, ident))
    return 'ExtResource("%s")' % ident


def add_sub(typ, props, prefix):
    key = (typ, tuple(props))
    if key in _dedupe:
        return 'SubResource("%s")' % _dedupe[key]
    ident = "%s_%d" % (prefix, len(subs) + 1)
    _dedupe[key] = ident
    subs.append((typ, ident, props))
    return 'SubResource("%s")' % ident


def flat(prefix, bg=None, border=None, bw=0, radii=None, detail=None, skew=0.0, shadow=None,
         margins=None, center=True, expand=None, bw_sides=None):
    """A StyleBoxFlat. Colours are palette names. radii = (tl, tr, br, bl); detail = corner_detail
    (1 draws a straight 45 degree bevel, 6.3); shadow = (dx, dy, size, colour); margins and expand
    are (left, top, right, bottom)."""
    p = []
    if margins:
        l, t, r, b = margins
        p += [("content_margin_left", float(l)), ("content_margin_top", float(t)),
              ("content_margin_right", float(r)), ("content_margin_bottom", float(b))]
    if not center:
        p.append(("draw_center", "false"))
    if bg is not None:
        p.append(("bg_color", C(bg)))
    if skew:
        p.append(("skew", "Vector2(%.4f, 0)" % skew))
    if bw or bw_sides:
        l, t, r, b = bw_sides or (bw, bw, bw, bw)
        p += [("border_width_left", l), ("border_width_top", t),
              ("border_width_right", r), ("border_width_bottom", b)]
        p.append(("border_color", C(border)))
    if radii:
        tl, tr, br, bl = radii
        p += [("corner_radius_top_left", tl), ("corner_radius_top_right", tr),
              ("corner_radius_bottom_right", br), ("corner_radius_bottom_left", bl)]
    if detail is not None:
        p.append(("corner_detail", detail))
    if expand:
        l, t, r, b = expand
        p += [("expand_margin_left", float(l)), ("expand_margin_top", float(t)),
              ("expand_margin_right", float(r)), ("expand_margin_bottom", float(b))]
    if shadow:
        dx, dy, size, scol = shadow
        p += [("shadow_color", C(scol)), ("shadow_size", size),
              ("shadow_offset", "Vector2(%g, %g)" % (dx, dy))]
    return add_sub("StyleBoxFlat", p, prefix)


PULP_KIT_DIR = "res://assets/sprites/ui/pulp/"
_PULP_KIT = json.load(open(os.path.join(ROOT, "assets", "sprites", "ui", "pulp", "pulp_kit.json"), encoding="utf-8"))
_tex_ids = {}


def kit_tex(stem):
    """An ExtResource for a frozen pulp kit PNG (catalog key = file stem): the pulp-only plates until the closeout."""
    if stem not in _tex_ids:
        ident = "kit_" + stem
        ext.append(("Texture2D", PULP_KIT_DIR + _PULP_KIT[stem]["file"], ident))
        _tex_ids[stem] = 'ExtResource("%s")' % ident
    return _tex_ids[stem]


def tbox(prefix, stem, content=None):
    """A StyleBoxTexture from the frozen pulp kit (margins and expand margins come from pulp_kit.json)."""
    entry = _PULP_KIT[stem]
    p = []
    if content:
        l, t, r, b = content
        p += [("content_margin_left", float(l)), ("content_margin_top", float(t)),
              ("content_margin_right", float(r)), ("content_margin_bottom", float(b))]
    p.append(("texture", kit_tex(stem)))
    l, t, r, b = entry["nine_slice_margins_LTRB"]
    p += [("texture_margin_left", float(l)), ("texture_margin_top", float(t)),
          ("texture_margin_right", float(r)), ("texture_margin_bottom", float(b))]
    if "expand_margins_LTRB" in entry:
        l, t, r, b = entry["expand_margins_LTRB"]
        p += [("expand_margin_left", float(l)), ("expand_margin_top", float(t)),
              ("expand_margin_right", float(r)), ("expand_margin_bottom", float(b))]
    if entry.get("axis_stretch") == "tile":
        p.append(("axis_stretch_horizontal", 1))
    return add_sub("StyleBoxTexture", p, prefix)


def empty(prefix="Empty"):
    return add_sub("StyleBoxEmpty", [], prefix)


def line(color, thickness):
    return add_sub("StyleBoxLine", [("color", C(color)), ("thickness", thickness)], "Line")


# ---- glass shapes in the 6.2 StyleBoxFlat fallback form ---------------------
SLAB_CHAMFER = 18          # G1: chamfer 18 at top-left and bottom-right
FIELD_CHAMFER = 8          # 3.3: the glass field
CHEVRON_RADIUS = 35        # the right corners bevel to a point at mid-height for any height up to 70 px
                           # (StyleBoxFlat shrinks two radii that exceed a side to fit it), a run of h / 2


def slab(prefix, padding, shadow=True):
    """G1 glass slab (6.2's flat fallback: a 45 degree bevel at TL/BR, a 2 px Edge border, the flat fill,
    the soft drop shadow). padding = (top, sides, bottom), the 3.1 preset."""
    top, sides, bottom = padding
    return flat(prefix, bg="GlassFillFlat", border="Edge", bw=2,
                radii=(SLAB_CHAMFER, 0, SLAB_CHAMFER, 0), detail=1,
                shadow=(0, 14, 22, "PanelShadow") if shadow else None,
                margins=(sides, top, sides, bottom))


def tag_plate(prefix):
    """G2 panel tag (flat): the gold wash with a 2 px lit bar on its left; the right end bevels at the bottom."""
    return flat(prefix, bg="TagWash", border="EdgeHi", bw_sides=(2, 0, 0, 0),
                radii=(0, 0, 8, 0), detail=1, margins=(14, 3, 14, 3))


def chevron_set(prefix, margins):
    """G3 chevron button per state (3.2). The focus box is an overlay (lit edge and glow, no base: plan 0.4 P5),
    so a focused, pressed row reads as pressed; hover lights the edge over the focus base (AC8, see the handoff)."""
    radii = (0, CHEVRON_RADIUS, CHEVRON_RADIUS, 0)
    return {
        "normal": flat(prefix + "Normal", bg="ButtonFill", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "hover": flat(prefix + "Hover", bg="ButtonFillFocus", border="Focus", bw=2, radii=radii, detail=1, margins=margins),
        "pressed": flat(prefix + "Pressed", bg="ButtonFillPressed", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "hover_pressed": flat(prefix + "Pressed", bg="ButtonFillPressed", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "disabled": flat(prefix + "Disabled", bg="ButtonFillDisabled", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "focus": flat(prefix + "Focus", border="Focus", bw=2, radii=radii, detail=1, center=False,
                      shadow=(0, 0, 14, "FocusGlow"), margins=margins),
        # Not a box Godot draws by itself: the button decor swaps it in for the current value of a list (3.2 selected).
        "selected": flat(prefix + "Selected", bg="ButtonFill", border="Edge", bw=2, radii=radii, detail=1, margins=margins),
    }


def field_set(prefix, margins):
    """The glass field (3.3: chamfer 8, a quiet edge, the button fill); focus is the lit edge and the glow."""
    radii = (FIELD_CHAMFER, 0, FIELD_CHAMFER, 0)
    return {
        "normal": flat(prefix + "Normal", bg="ButtonFill", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "hover": flat(prefix + "Hover", bg="ButtonFillFocus", border="Edge", bw=2, radii=radii, detail=1, margins=margins),
        "pressed": flat(prefix + "Pressed", bg="ButtonFillPressed", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "hover_pressed": flat(prefix + "Pressed", bg="ButtonFillPressed", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "disabled": flat(prefix + "Disabled", bg="ButtonFillDisabled", border="EdgeDim", bw=2, radii=radii, detail=1, margins=margins),
        "focus": flat(prefix + "Focus", border="Focus", bw=2, radii=radii, detail=1, center=False,
                      shadow=(0, 0, 14, "FocusGlow"), margins=margins),
    }


TAB_MARGINS = (26, 9, 26, 9)   # 3.3: padding 12 / 26, 48 tall with Outfit's natural line height


def tab_box(prefix, bg):
    """G14 tab (flat): the top-right corner bevels 16 px (the top edge is the shorter)."""
    return flat(prefix, bg=bg, radii=(0, 16, 0, 0), detail=1, margins=TAB_MARGINS)


def tab_focus(prefix):
    return flat(prefix, radii=(0, 16, 0, 0), detail=1, center=False, shadow=(0, 0, 14, "FocusGlow"), margins=TAB_MARGINS)


# ---- fonts -----------------------------------------------------------------
CINZEL = add_ext("FontFile", "res://assets/fonts/cinzel/Cinzel-Variable.ttf", "font_cinzel")
OUTFIT = add_ext("FontFile", "res://assets/fonts/outfit/Outfit-Variable.ttf", "font_outfit")
WGHT, TNUM = 2003265652, 1953063277   # OpenType tags 'wght' and 'tnum' as ints
CHECK_ON = add_ext("Texture2D", "res://assets/ui/theme_placeholder/check_on.png", "tex_check_on")
CHECK_OFF = add_ext("Texture2D", "res://assets/ui/theme_placeholder/check_off.png", "tex_check_off")
RADIO_ON = add_ext("Texture2D", "res://assets/ui/theme_placeholder/radio_on.png", "tex_radio_on")
RADIO_OFF = add_ext("Texture2D", "res://assets/ui/theme_placeholder/radio_off.png", "tex_radio_off")

# The faces' natural line heights (ascender - descender, from their hhea / OS2 tables, units per em 1000):
# Godot's Label line height. A role's 2.3 line height becomes line_spacing = lh * size - natural height.
NATURAL_LINE = {"outfit": 1.26, "cinzel": 1.348}

_fonts = {}


def font(face, wght, spacing=0, tnum=False, default=False):
    key = (face, wght, spacing, tnum, default)
    if key in _fonts:
        return _fonts[key]
    base = CINZEL if face == "cinzel" else OUTFIT
    p = []
    if default:
        p.append(("resource_name", '"ftt_default_font_outfit_400"'))
    p += [("base_font", base), ("variation_opentype", "{\n%d: %s\n}" % (WGHT, float(wght)))]
    if spacing:
        p.append(("spacing_glyph", spacing))
    if tnum:
        p.append(("opentype_features", "{\n%d: 1\n}" % TNUM))
    ref = add_sub("FontVariation", p, "FontVar")
    _fonts[key] = ref
    return ref


def tracking_px(em, size):
    """CSS letter-spacing (em) as FontVariation.spacing_glyph (whole px, 2.5)."""
    return int(round(em * size + 1e-9))


# ---- theme item collection --------------------------------------------------
items = []    # (key, value)


def put(key, value):
    items.append((key, value))


def colors(type_, **kv):
    for k, v in kv.items():
        put("%s/colors/%s" % (type_, k), C(v))


# ---- Label type roles (UI_SPEC_GLASS 2.3; sizes rounded to whole px, the plan's 2.2 list) -------
# Halo tiers (2.4): None = plate text (on glass, no halo); "S" = halo S; "S6" = halo S with
# shadow_outline_size 6 (T29); "L" = halo L. lh = the 2.3 line height, applied as line_spacing only
# on roles whose text wraps (single-line roles never need it: a Label is never shorter than its font).
# name, face, wght (drawn), px, tracking em, colour, halo, tnum, lh, spec
ROLES = [
    # the four type-scale roles (9.1)
    ("SmallLabel",         "outfit", 500, 18, 0.0,  "Ink2", None, False, None, "micro copy"),
    ("HeadingLabel",       "cinzel", 700, 26, 0.13, "Accent", None, False, None, "section heading inside a slab (9.1)"),
    ("EmphasisLabel",      "outfit", 600, 30, 0.0,  "Ink",  None, False, None, "the emphasis step of the scale"),
    ("TitleLabel",         "cinzel", 900, 36, 0.05, "Ink",  None, False, None, "panel title inside a slab (9.1)"),
    # menu, headings, pause
    ("CoverTitle",         "cinzel", 900, 112, 0.05, "TitleMid", "L", False, None, "T01 (flat fallback; TitleGradient multiplies a white face)"),
    ("CoverBanner",        "cinzel", 700, 38, 0.6,  "Ink2", None, False, None, "T02"),
    ("BannerHeading",      "cinzel", 900, 56, 0.05, "Ink",  "L", False, None, "T03"),
    ("PauseTitle",         "cinzel", 900, 78, 0.14, "TextFocus", "L", False, None, "T04"),
    ("LevelTitle",         "cinzel", 500, 26, 0.12, "Ink",  "S", False, None, "T05"),
    ("ObjectiveLine",      "outfit", 400, 22, 0.0,  "Res2", "S", False, None, "T06"),
    ("PanelTag",           "outfit", 500, 18, 0.16, "Ink",  None, False, None, "T07"),
    ("HeroName",           "cinzel", 700, 22, 0.13, "Ink",  None, False, None, "T08"),
    ("HeroNameScene",      "cinzel", 700, 28, 0.13, "Ink",  "S", False, None, "T09"),
    ("PromptKey",          "outfit", 700, 19, 0.0,  "TextFocus", None, False, None, "T12"),
    ("PromptCaption",      "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T13"),
    ("CaptionLabel",       "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T14 on glass, T48"),
    ("SceneCaption",       "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T14 on the scene"),
    ("VersionCaption",     "outfit", 500, 18, 0.16, "Ink3", "S", False, None, "T15 (17 raised to 18)"),
    ("StatusLine",         "outfit", 400, 18, 0.06, "Ink2", None, False, None, "T16"),
    # HUD
    ("HudNumeral",         "outfit", 600, 32, 0.0,  "TextFocus", "S", True, None, "T17, the Fighter numeral (31.68)"),
    ("HudNumeralSmall",    "outfit", 600, 22, 0.0,  "TextFocus", "S", True, None, "T17, the Story numeral (21.6); the Fighter /max"),
    ("HudNumeralMax",      "outfit", 400, 18, 0.0,  "TextFocus", "S", True, None, "T17 / T32 /max (AC3; the widget dims it to .78 / .75)"),
    ("SlotKeyTag",         "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T18 in Story (a plain label)"),
    ("SlotKeyTagScene",    "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T18 over the scene (Fighter)"),
    ("SlotNumber",         "outfit", 700, 19, 0.0,  "TextFocus", "S", True, None, "T19 for the 54 px lens (0.36 x lens)"),
    ("StatusName",         "outfit", 500, 18, 0.16, "Ink",  "S", False, None, "T20"),
    ("StatusTime",         "outfit", 600, 21, 0.0,  "Ink2", "S", False, None, "T21 (20.7)"),
    ("DefyLabel",          "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T22 (16.2 raised to 18)"),
    ("RewindCount",        "outfit", 600, 32, 0.0,  "Ink",  None, True,  None, "T23 (31.5)"),
    ("StateLabel",         "outfit", 600, 20, 0.10, "Ink",  None, False, None, "T25 (19.8)"),
    ("DustTotal",          "outfit", 600, 38, 0.02, "Ink",  None, True,  None, "T26 (37.5)"),
    ("DustFloat",          "outfit", 600, 26, 0.02, "Res2", None, True,  None, "T27 (25.5)"),
    ("IntegrityPercent",   "outfit", 600, 23, 0.0,  "Ink",  "S", True,  None, "T28 (23.1)"),
    ("IntegrityCaption",   "outfit", 500, 18, 0.16, "Ink",  "S6", False, 1.15, "T29"),
    ("BossName",           "cinzel", 900, 38, 0.08, "TextFocus", "L", False, None, "T30"),
    ("BossPhaseTag",       "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T31 (a plain label)"),
    ("BossHpValue",        "outfit", 400, 24, 0.0,  "TextFocus", "S", True, None, "T32"),
    ("NoteLabel",          "outfit", 500, 18, 0.16, "Ink2", "S", False, 1.35, "T33"),
    ("TimerDigits",        "outfit", 600, 60, 0.04, "Ink",  None, True,  None, "T34"),
    # select
    ("CardName",           "outfit", 600, 18, 0.16, "Ink2", None, False, None, "T35 the tile name (17 raised to 18)"),
    ("CardDates",          "outfit", 500, 18, 0.12, "Accent", None, False, None, "T36 retired (pulp values kept until the closeout)"),
    ("CardStamp",          "outfit", 600, 26, 0.20, "Accent", None, False, None, "T37 the READY badge text"),
    ("CardQuestion",       "cinzel", 700, 40, 0.0,  "Ink2", None, False, None, "T38"),
    ("BubbleText",         "outfit", 400, 46, 0.04, "LabelDark", None, False, None, "T39 retired (pulp values kept until the closeout)"),
    ("SideCardName",       "cinzel", 900, 48, 0.05, "TextFocus", "L", False, 1.08, "T40"),
    ("StatLabel",          "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T41"),
    ("StatValue",          "outfit", 600, 24, 0.0,  "Ink",  None, True,  None, "T42"),
    ("StarBadgeText",      "outfit", 400, 44, 0.0,  "LabelDark", None, False, None, "T43 retired (pulp values kept until the closeout)"),
    ("HintLine",           "outfit", 400, 22, 0.0,  "Ink2", "S", False, None, "T44"),
    ("HintLinePlate",      "outfit", 400, 22, 0.0,  "Ink2", None, False, None, "T44 on the glass tag plate (the credits skip hint)"),
    ("StickerLine1",       "cinzel", 700, 24, 0.13, "Ink",  None, False, None, "T45 the Continue card's hero name"),
    ("StickerLine2",       "outfit", 400, 22, 0.0,  "Ink",  None, False, None, "T46 the Continue card's place line"),
    ("StickerLine3",       "outfit", 400, 19, 0.0,  "Ink2", None, False, None, "T47 the Continue card's meta line"),
    # dialogue
    ("DialogueBody",       "outfit", 400, 33, 0.0,  "Ink",  None, False, 1.28, "T49"),
    ("SpeakerName",        "cinzel", 900, 30, 0.10, "Accent", None, False, None, "T50"),
    # glass-only roles (T51-T60, 9.1)
    ("MenuTagline",        "outfit", 500, 21, 0.42, "Accent", None, False, None, "T51 (the loading_rift line; the menu shows no tagline, R3)"),
    ("SettingsRowLabel",   "outfit", 500, 22, 0.10, "Ink",  None, False, None, "T52"),
    ("SettingsRowValue",   "outfit", 600, 26, 0.0,  "Ink",  None, True,  None, "T53"),
    ("ToggleText",         "outfit", 700, 18, 0.12, "Ink2", None, False, None, "T54 (17 raised to 18; ON draws white)"),
    ("TabLabel",           "outfit", 600, 24, 0.13, "Ink2", None, False, None, "T55"),
    ("AbilityName",        "outfit", 500, 18, 0.0,  "Ink2", None, False, 1.15, "T56 (14 raised to 18, no tracking: D9)"),
    ("RandomWord",         "outfit", 600, 20, 0.30, "Ink2", None, False, None, "T57"),
    ("SelectCountdown",    "outfit", 600, 26, 0.20, "Accent", "S", False, None, "T59"),
    ("UltimateReadyLabel", "outfit", 700, 18, 0.16, "Res2", "S", False, None, "T60"),
    ("BodyLabel",          "outfit", 400, 26, 0.0,  "Ink",  None, False, 1.35, "9.1 body copy"),
    ("BodySmallLabel",     "outfit", 400, 22, 0.0,  "Ink",  None, False, None, "9.1 small copy"),
]

# halo tier -> (outline px, outline colour, shadow offset y, shadow outline px, shadow colour)  (2.4)
HALOS = {
    "S": (2, "HaloOutlineS", 1, 4, "HaloShadowS"),
    "S6": (2, "HaloOutlineS", 1, 6, "HaloShadowS"),
    "L": (3, "HaloOutlineL", 3, 8, "HaloShadowL"),
}


def line_spacing(face, size, lh):
    return int(round(lh * size - NATURAL_LINE[face] * size))


def emit_label_roles():
    for (name, face, wght, size, track, color, halo, tnum, lh, _spec) in ROLES:
        put("%s/base_type" % name, '&"Label"')
        put("%s/fonts/font" % name, font(face, wght, tracking_px(track, size), tnum))
        put("%s/font_sizes/font_size" % name, size)
        put("%s/colors/font_color" % name, C(color))
        if halo:
            outline, outline_col, dy, shadow_outline, shadow_col = HALOS[halo]
            put("%s/colors/font_outline_color" % name, C(outline_col))
            put("%s/constants/outline_size" % name, outline)
            put("%s/colors/font_shadow_color" % name, C(shadow_col))
            put("%s/constants/shadow_offset_x" % name, 0)
            put("%s/constants/shadow_offset_y" % name, dy)
            put("%s/constants/shadow_outline_size" % name, shadow_outline)
        if lh is not None:
            put("%s/constants/line_spacing" % name, line_spacing(face, size, lh))
        if name == "HintLinePlate":
            # T44 on its own glass tag plate (the credits skip hint, 9.2).
            put("%s/styles/normal" % name, tag_plate("HintPlate"))


CHEVRON_COLORS = dict(font_color="Ink2", font_hover_color="TextFocus", font_focus_color="TextFocus",
                      font_pressed_color="TextFocus", font_hover_pressed_color="TextFocus",
                      font_disabled_color="Ink3")
FIELD_COLORS = dict(font_color="Ink", font_hover_color="TextFocus", font_focus_color="TextFocus",
                    font_pressed_color="TextFocus", font_hover_pressed_color="TextFocus",
                    font_disabled_color="Ink3")


def put_button(type_, base, boxes, size, font_ref, color_map):
    if base:
        put("%s/base_type" % type_, '&"%s"' % base)
    for state, ref in boxes.items():
        put("%s/styles/%s" % (type_, state), ref)
    colors(type_, **color_map)
    put("%s/font_sizes/font_size" % type_, size)
    put("%s/fonts/font" % type_, font_ref)


# The 3.1 slab presets: name -> (top, sides, bottom).
PANEL_PRESETS = [
    ("GlassPanelPause", (30, 20, 28), "the pause panel"),
    ("GlassPanelVitals", (20, 26, 20), "the Story Vitals panel"),
    ("GlassPanelAbilities", (22, 30, 14), "the Story Abilities panel"),
    ("GlassPanelTimeTools", (14, 26, 14), "the Story time tools panel"),
    ("GlassPanelDust", (14, 24, 14), "the dust plate"),
    ("GlassPanelTimer", (12, 32, 12), "the Fighter timer plate"),
    ("GlassPanelStats", (20, 26, 18), "the select stat panel"),
    ("GlassPanelContinue", (16, 20, 16), "the main menu Continue card"),
]
DEFAULT_PANEL_PADDING = (22, 28, 22)
DIALOGUE_PANEL_PADDING = (22, 26, 22)

# Chevron variations: name -> (content margins L T R B, font size, tracking em). The label starts at x + 58
# (compact + 52) and ends 54 (compact 40) before the right edge (3.2); the vertical margins centre the
# Cinzel line in the drawn heights (68, 64, 58).
CHEVRONS = [
    ("PulpButton", (58, 11, 54, 11), 34, 0.13, "T10 menu chevron row, 68 px"),
    ("TemporalGlassButton", (58, 9, 54, 9), 34, 0.13, "T10 pause and list chevron row, 64 px"),
    ("PulpButtonCompact", (52, 13, 40, 13), 24, 0.13, "T11 compact chevron, 58 px"),
]
FIELD_MARGINS = (16, 8, 14, 8)   # 3.3 option button: value text at left 16, the arrow at right 14; 44 px with Outfit 22


def build():
    default_font = font("outfit", 400, default=True)

    # ---- Buttons: the three chevrons, the glass field (default Button, OptionButton, GlassField), the tab buttons
    for name, margins, size, track, _doc in CHEVRONS:
        put_button(name, "Button", chevron_set(name, margins), size,
                   font("cinzel", 700, tracking_px(track, size)), CHEVRON_COLORS)
    field = field_set("Field", FIELD_MARGINS)
    field_font = font("outfit", 600)
    put_button("Button", None, field, 22, field_font, FIELD_COLORS)
    put_button("GlassField", "Button", field, 22, field_font, FIELD_COLORS)
    put_button("OptionButton", None, field, 22, field_font, FIELD_COLORS)
    put("OptionButton/constants/arrow_margin", 14)
    tab_font = font("outfit", 600, tracking_px(0.13, 24))
    unselected, hovered, selected = tab_box("TabUn", "TabFill"), tab_box("TabHov", "TabHover"), tab_box("TabSel", "Accent")
    put_button("GlassTab", "Button", {
        "normal": unselected, "hover": hovered, "pressed": selected, "hover_pressed": selected,
        "disabled": unselected, "focus": tab_focus("TabFocus")}, 24, tab_font,
        dict(font_color="Ink2", font_hover_color="Ink", font_focus_color="Ink2", font_pressed_color="TabSelectedInk",
             font_hover_pressed_color="TabSelectedInk", font_disabled_color="Ink3"))
    put_button("GlassTabSelected", "Button", {
        "normal": selected, "hover": selected, "pressed": selected, "hover_pressed": selected,
        "disabled": selected, "focus": tab_focus("TabFocus")}, 24, tab_font,
        dict(font_color="TabSelectedInk", font_hover_color="TabSelectedInk", font_focus_color="TabSelectedInk",
             font_pressed_color="TabSelectedInk", font_hover_pressed_color="TabSelectedInk",
             font_disabled_color="TabSelectedInk"))

    # CheckBox (no shipped screen uses one; CheckButton stays unthemed for PulpToggleDecor): a lit focus ring.
    ring = flat("Ring", border="Focus", bw=2, center=False, expand=(3, 3, 3, 3))
    put("CheckBox/styles/focus", ring)
    for state in ("normal", "hover", "pressed", "hover_pressed", "disabled"):
        put("CheckBox/styles/" + state, empty("CheckBoxNone"))
    colors("CheckBox", font_color="Ink", font_hover_color="TextFocus", font_focus_color="TextFocus",
           font_pressed_color="TextFocus", font_hover_pressed_color="TextFocus", font_disabled_color="Ink3")
    put("CheckBox/font_sizes/font_size", 20)
    put("CheckBox/constants/h_separation", 12)
    for name, tex in (("checked", CHECK_ON), ("unchecked", CHECK_OFF),
                      ("radio_checked", RADIO_ON), ("radio_unchecked", RADIO_OFF),
                      ("checked_disabled", CHECK_ON), ("unchecked_disabled", CHECK_OFF),
                      ("radio_checked_disabled", RADIO_ON), ("radio_unchecked_disabled", RADIO_OFF)):
        put("CheckBox/icons/" + name, tex)

    # ---- Labels
    colors("Label", font_color="Ink")
    put("Label/font_sizes/font_size", 20)
    emit_label_roles()

    # ---- RichTextLabel: the dialogue line (T49; the Label role DialogueBody cannot reach a RichTextLabel)
    put("DialogueBodyRich/base_type", '&"RichTextLabel"')
    put("DialogueBodyRich/fonts/normal_font", font("outfit", 400, 0))
    put("DialogueBodyRich/font_sizes/normal_font_size", 33)
    put("DialogueBodyRich/colors/default_color", C("Ink"))
    put("DialogueBodyRich/constants/line_separation", line_spacing("outfit", 33, 1.28))

    # ---- Panels
    default_slab = slab("Slab", DEFAULT_PANEL_PADDING)
    put("Panel/styles/panel", default_slab)
    put("PanelContainer/styles/panel", default_slab)
    put("PopupPanel/styles/panel", slab("PopupSlab", (8, 8, 8), shadow=False))

    def pv(name, ref):
        put(name + "/base_type", '&"PanelContainer"')
        put(name + "/styles/panel", ref)

    pv("InkPanel", default_slab)
    pv("HudPlatePanel", default_slab)
    pv("DialogueGlassPanel", slab("DialogueSlab", DIALOGUE_PANEL_PADDING))
    for name, padding, _doc in PANEL_PRESETS:
        pv(name, slab(name, padding))
    pv("PanelTagPlate", tag_plate("Tag"))
    # The dim is painted opaque; every ScrimPanel user multiplies it by SelfModulate alpha 0.70
    # (PulpScreenKit.ScrimAlpha and the three pause scenes), which gives the build dim rgba(3, 5, 14, .70) (1.4).
    pv("ScrimPanel", flat("Scrim", bg="ScrimFill", margins=(0, 0, 0, 0)))
    # G12 READY badge (6.2): its glow is a StyleBoxFlat shadow, which also fills under the translucent badge.
    pv("ReadyBadge", flat("ReadyBadge", bg="ReadyBadgeFill", border="Accent", bw=2,
                          shadow=(0, 0, 18, "ReadyBadgeGlow"), margins=(22, 8, 22, 8)))
    pv("ReadyBadgeIdle", flat("ReadyBadgeIdle", bg="ReadyBadgeIdleFill", border="ReadyBadgeIdleEdge", bw=2,
                              margins=(22, 8, 22, 8)))
    # Pulp-only plates: unchanged (still the pulp art, so unrestyled screens render) until the closeout retires them.
    pv("PaperPanel", tbox("Paper", "panel_paper", (22, 18, 28, 28)))
    pv("BannerPanel", tbox("Banner", "banner_select", (64, 8, 70, 10)))
    pv("CoverBannerPanel", tbox("CoverBanner", "banner_cover", (30, 6, 44, 14)))
    pv("BossNameplate", tbox("BossName", "boss_nameplate", (26, 6, 30, 10)))
    pv("BossPhaseTab", tbox("BossPhase", "boss_phase_tab", (14, 5, 16, 6)))
    pv("SlotPlate", tbox("Slot", "slot_frame", (6, 6, 8, 8)))
    pv("SlotPlateReady", tbox("SlotReady", "slot_frame_ready", (6, 6, 8, 8)))
    pv("KeyTagPlate", tbox("KeyTag", "key_tag", (7, 3, 7, 2)))
    pv("CardPlate", tbox("Card", "card_frame", (10, 10, 14, 14)))
    pv("CardPlatePick", tbox("CardPick", "card_frame_pick", (10, 10, 14, 14)))
    pv("BubblePlate", tbox("Bubble", "bubble", (26, 10, 32, 14)))
    pv("StampPlate", tbox("Stamp", "stamp_ready", (16, 6, 20, 8)))

    # ---- Inputs: the glass field (LineEdit), sliders, scroll bars, spin box buttons
    line_margins = (16, 8, 16, 8)
    radii = (FIELD_CHAMFER, 0, FIELD_CHAMFER, 0)
    colors("LineEdit", font_color="Ink", font_placeholder_color="Ink3", caret_color="Accent",
           font_selected_color="Ink", selection_color="Selection")
    put("LineEdit/font_sizes/font_size", 22)
    put("LineEdit/styles/normal", flat("LineN", bg="ButtonFill", border="EdgeDim", bw=2, radii=radii, detail=1, margins=line_margins))
    put("LineEdit/styles/focus", flat("LineF", border="Focus", bw=2, radii=radii, detail=1, center=False,
                                      shadow=(0, 0, 14, "FocusGlow"), margins=line_margins))
    put("LineEdit/styles/read_only", flat("LineR", bg="ButtonFillDisabled", border="EdgeDim", bw=2, radii=radii, detail=1,
                                          margins=line_margins))
    # 3.3 slider: a 12 px track (content margins 6 and 6) leaning with a 6 px run (skew 0.5). UR-POLISH: the track ends
    # under the grabber's travel limit (expand right -6), so its slanted cap never shows past the grabber at a high value.
    put("HSlider/styles/slider", flat("Slider", bg="SliderTrack", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, -6, 0)))
    put("HSlider/styles/grabber_area", flat("SliderFill", bg="Accent", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, 0, 0)))
    put("HSlider/styles/grabber_area_highlight", flat("SliderFillHi", bg="Focus", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, 0, 0)))
    put("VSlider/styles/slider", flat("VSlider", bg="SliderTrack", margins=(6, 0, 6, 0)))
    put("VSlider/styles/grabber_area", flat("VSliderFill", bg="Accent", margins=(6, 0, 6, 0)))
    put("VSlider/styles/grabber_area_highlight", flat("VSliderFillHi", bg="Focus", margins=(6, 0, 6, 0)))
    for t in ("HSlider", "VSlider"):
        # Step 1 keeps the frozen pulp grabber icon; step 2 wires glass_slider_grabber.png.
        for icon in ("grabber", "grabber_highlight", "grabber_disabled"):
            put(t + "/icons/" + icon, kit_tex("slider_grabber"))
    # 3.3 scroll bar: 8 px wide, rounded ends, a gold grabber at least 48 long.
    for t, track_margins, grab_margins in (("VScrollBar", (4, 0, 4, 0), (4, 24, 4, 24)),
                                          ("HScrollBar", (0, 4, 0, 4), (24, 4, 24, 4))):
        scroll = flat(t + "Track", bg="ScrollTrack", radii=(4, 4, 4, 4), margins=track_margins)
        put(t + "/styles/scroll", scroll)
        put(t + "/styles/scroll_focus", scroll)
        put(t + "/styles/grabber", flat(t + "Grab", bg="Edge", radii=(4, 4, 4, 4), margins=grab_margins))
        put(t + "/styles/grabber_highlight", flat(t + "GrabHi", bg="Focus", radii=(4, 4, 4, 4), margins=grab_margins))
        put(t + "/styles/grabber_pressed", flat(t + "GrabHi", bg="Focus", radii=(4, 4, 4, 4), margins=grab_margins))
    for d in ("up", "down"):
        put("SpinBox/styles/%s_background" % d, empty("SpinNone"))
        put("SpinBox/styles/%s_background_disabled" % d, empty("SpinNone"))
        put("SpinBox/styles/%s_background_hovered" % d, flat("SpinHover", bg="RingTrack"))
        put("SpinBox/styles/%s_background_pressed" % d, flat("SpinPressed", bg="ButtonFillPressed"))

    # ---- Progress bars. The fill stays a StyleBoxFlat (FighterHUD.TintFill recolours it; the bar decor reads it).
    # The default ProgressBar wears the glass tube's dark track and gold edge. The five HUD bar variations keep their
    # pulp tracks and skewed fills for now: the pulp bar decor draws its layers over the bar's own fill and reads the
    # fill's skew, so their glass form (an empty background and a fill that holds its colour but draws nothing,
    # plan 2.2) can only land together with the glass tube decor (TG-WIDGETS step 2). See the handoff.
    colors("ProgressBar", font_color="Ink")
    put("ProgressBar/font_sizes/font_size", 18)
    put("ProgressBar/styles/background", flat("ProgBg", bg="TubeTrackTop", border="Edge", bw=2))
    put("ProgressBar/styles/fill", flat("ProgFill", bg="Accent"))
    put("PulpBar/base_type", '&"ProgressBar"')
    put("PulpBar/styles/background", tbox("BarBg", "bar_track_h30", (3, 3, 3, 3)))
    put("PulpBar/styles/fill", flat("BarFill", bg="Hp", skew=0.2126))
    colors("PulpBar", font_color="Ink")
    put("PulpBar/font_sizes/font_size", 22)
    for name, stem, margins, fill_color in (("PulpBarH44", "bar_track_h44", None, "Hp"),
                                            ("PulpBarH16", "bar_track_h16", None, "Meter2"),
                                            ("PulpBarH14", "bar_track_h14", (3, 3, 3, 3), "Accent")):
        put("%s/base_type" % name, '&"ProgressBar"')
        put("%s/styles/background" % name, tbox(name + "Bg", stem, margins))
        put("%s/styles/fill" % name, flat(name + "Fill", bg=fill_color, skew=0.2126))
        colors(name, font_color="Ink")
        put("%s/font_sizes/font_size" % name, 22)
    put("PulpBossBar/base_type", '&"ProgressBar"')
    put("PulpBossBar/styles/background", tbox("BossBarBg", "bar_track_boss", (5, 5, 5, 5)))
    put("PulpBossBar/styles/fill", flat("BossBarFill", bg="Danger", skew=0.1763))
    colors("PulpBossBar", font_color="TextFocus")
    put("PulpBossBar/font_sizes/font_size", 24)

    # ---- Tabs (G14): TabContainer's own styles; the inner panel is a quiet field that keeps the pulp content
    # margins (the Settings layout is TG-PAUSE's).
    put("TabContainer/styles/panel", flat("TabPanel", bg="TabPanelFill", border="EdgeDim", bw=2, radii=(12, 0, 12, 0),
                                          detail=1, margins=(18, 18, 24, 26)))
    put("TabContainer/styles/tab_selected", selected)
    put("TabContainer/styles/tab_unselected", unselected)
    put("TabContainer/styles/tab_hovered", hovered)
    put("TabContainer/styles/tab_disabled", unselected)
    put("TabContainer/styles/tab_focus", tab_focus("TabFocus"))
    put("TabContainer/styles/tabbar_background", empty("TabBar"))
    colors("TabContainer", font_selected_color="TabSelectedInk", font_hovered_color="Ink",
           font_unselected_color="Ink2", font_disabled_color="Ink3")
    put("TabContainer/font_sizes/font_size", 24)
    put("TabContainer/fonts/font", tab_font)

    # ---- Popup menu (a glass slab of 44 px rows), tooltip, separators
    put("PopupMenu/styles/panel", slab("PopupSlab", (8, 8, 8), shadow=False))
    put("PopupMenu/styles/hover", flat("MenuHover", bg="FocusWash", border="Focus", bw_sides=(2, 0, 0, 0),
                                       margins=(8, 4, 8, 4)))
    put("PopupMenu/styles/separator", line("EdgeDim", 1))
    colors("PopupMenu", font_color="Ink", font_hover_color="TextFocus", font_disabled_color="Ink3",
           font_separator_color="Ink3")
    put("PopupMenu/font_sizes/font_size", 22)
    put("PopupMenu/fonts/font", font("outfit", 500))
    put("PopupMenu/constants/v_separation", 16)
    put("PopupMenu/constants/item_start_padding", 16)
    put("PopupMenu/constants/item_end_padding", 16)
    put("TooltipPanel/styles/panel", flat("Tip", bg="GlassFillFlat", border="Edge", bw=2,
                                          radii=(FIELD_CHAMFER, 0, FIELD_CHAMFER, 0), detail=1, margins=(12, 6, 12, 6)))
    colors("TooltipLabel", font_color="Ink")
    put("TooltipLabel/font_sizes/font_size", 18)
    put("HSeparator/styles/separator", line("EdgeDim", 1))
    put("VSeparator/styles/separator", line("EdgeDim", 1))
    return default_font


def fmt_value(v):
    return v if isinstance(v, str) else str(v)


def write_theme(default_font):
    out = ['[gd_resource type="Theme" load_steps=%d format=3 uid="uid://c3ajip41tplop"]\n'
           % (len(ext) + len(subs) + 1)]
    for typ, path, ident in ext:
        out.append('[ext_resource type="%s" path="%s" id="%s"]' % (typ, path, ident))
    out.append("")
    for typ, ident, props in subs:
        out.append('[sub_resource type="%s" id="%s"]' % (typ, ident))
        for k, v in props:
            out.append("%s = %s" % (k, fmt_value(v)))
        out.append("")
    out.append("[resource]")
    out.append("default_font = %s" % default_font)
    out.append("default_font_size = 20")
    for k, v in sorted(items, key=lambda kv: kv[0]):
        out.append("%s = %s" % (k, fmt_value(v)))
    path = os.path.join(ROOT, "resources", "UI", "ftt_theme.tres")
    with open(path, "w", newline="\n") as f:
        f.write("\n".join(out) + "\n")


VARIATIONS = [
    ("PulpButton", "Button, T10: the menu chevron row (68 px; legacy name)"),
    ("PulpButtonCompact", "Button, T11: the compact chevron (58 px; legacy name)"),
    ("TemporalGlassButton", "Button, T10: the pause and list chevron row (64 px)"),
    ("GlassField", "Button: the glass field (3.3, chamfer 8), the same as the default Button"),
    ("GlassTab", "Button: a G14 tab (pressed = selected) for a tab row built from buttons"),
    ("GlassTabSelected", "Button: a G14 tab in its selected look"),
    ("InkPanel", "PanelContainer: the glass slab G1, default padding 22 / 28 / 22 (legacy name)"),
    ("HudPlatePanel", "PanelContainer: the glass slab G1 (no scene user; retired by the closeout)"),
    ("DialogueGlassPanel", "PanelContainer: the glass slab G1, dialogue padding 22 / 26 / 22"),
] + [(name, "PanelContainer: the glass slab G1 for %s, padding %d / %d / %d" % ((doc,) + padding))
     for name, padding, doc in PANEL_PRESETS] + [
    ("PanelTagPlate", "PanelContainer: the glass panel tag G2"),
    ("ScrimPanel", "PanelContainer: the pause dim, painted opaque; every user sets SelfModulate alpha 0.70"),
    ("ReadyBadge", "PanelContainer: the G12 READY badge"),
    ("ReadyBadgeIdle", "PanelContainer: the G12 badge while choosing"),
    ("PaperPanel", "PanelContainer, pulp-only (retired by the closeout): paper panel"),
    ("BannerPanel", "PanelContainer, pulp-only (retired by the closeout): gold banner"),
    ("BossNameplate", "PanelContainer, pulp-only (retired by the closeout): boss nameplate"),
    ("BossPhaseTab", "PanelContainer, pulp-only (retired by the closeout): boss PHASE tab"),
    ("SlotPlate", "PanelContainer, pulp-only (retired by the closeout): ability slot"),
    ("SlotPlateReady", "PanelContainer, pulp-only (retired by the closeout): ready slot"),
    ("KeyTagPlate", "PanelContainer, pulp-only (retired by the closeout): slot key tag"),
    ("CardPlate", "PanelContainer, pulp-only (retired by the closeout): trading card"),
    ("CardPlatePick", "PanelContainer, pulp-only (retired by the closeout): a picked card"),
    ("BubblePlate", "PanelContainer, pulp-only (retired by the closeout): speech bubble"),
    ("StampPlate", "PanelContainer, pulp-only (retired by the closeout): card stamp"),
    ("CoverBannerPanel", "PanelContainer, pulp-only (retired by the closeout): the cover's banner"),
    ("PulpBar", "ProgressBar: the Story HP bar, 30 px (the fill stays a StyleBoxFlat; the pulp track until the glass tube decor lands)"),
    ("PulpBarH44", "ProgressBar: the Fighter HP bar, 44 px (pulp track until the glass tube decor lands)"),
    ("PulpBarH16", "ProgressBar: the Fighter Ultimate bar, 16 px (pulp track until the glass tube decor lands)"),
    ("PulpBarH14", "ProgressBar: the select stat bar, 14 px (pulp track until the glass tube decor lands)"),
    ("PulpBossBar", "ProgressBar: the boss bar (pulp track until the glass tube decor lands)"),
    ("DialogueBodyRich", "RichTextLabel, T49: the dialogue line (Outfit 400, 33 px)"),
]


def cs_color(name):
    hexstr, alpha = PALETTE[name]
    r, g, b = hexstr[0:2], hexstr[2:4], hexstr[4:6]
    return "new Color(0x%s / 255f, 0x%s / 255f, 0x%s / 255f, %sf)" % (r, g, b, repr(float(alpha)))


def write_roles():
    lines = ["// <auto-generated> by tools/ui_theme/build_ftt_theme.py - edit the script, not this file.",
             "using Godot;",
             "",
             "namespace FTT.UI {",
             "    public static partial class UIPalette {",
             "        /// <summary>Label type-variation names authored in the shared theme (UI_SPEC_GLASS 2.3 roles).</summary>",
             "        public static class LabelRoles {"]
    for (name, face, wght, size, track, color, halo, tnum, lh, spec) in ROLES:
        lines.append("            /// <summary>%s %d, %d px, %.2f em, %s, %s (%s).</summary>"
                     % (face.capitalize(), wght, size, track, color, "halo " + halo if halo else "plate", spec))
        lines.append('            public const string %s = "%s";' % (name, name))
    lines += ["        }", "",
              "        /// <summary>",
              "        /// Every Label role: authored font size, pinned face and weight, tracking in em (spacing_glyph is it times the",
              "        /// size, rounded), and the scene-halo tier (\"\" plate text, \"S\", \"S6\" halo S with a 6 px shadow outline, \"L\").",
              "        /// </summary>",
              "        public static readonly (string Name, int Size, string Face, int Weight, float Tracking, string Halo)[] LabelRoleTable = {"]
    for (name, face, wght, size, track, color, halo, tnum, lh, spec) in ROLES:
        lines.append('            ("%s", %d, "%s", %d, %sf, "%s"),' % (name, size, face, wght, repr(float(track)), halo or ""))
    lines += ["        };", "",
              "        /// <summary>Non-Label variations authored in the shared theme.</summary>",
              "        public static class Variations {"]
    for name, doc in VARIATIONS:
        lines.append("            /// <summary>%s.</summary>" % doc)
        lines.append('            public const string %s = "%s";' % (name, name))
    lines += ["        }", "",
              "        /// <summary>",
              "        /// The generator's palette table (build_ftt_theme.py PALETTE): every colour the theme paints, under the name of",
              "        /// the UIPalette constant that mirrors it. UIThemeTests pins each entry to that constant.",
              "        /// </summary>",
              "        public static readonly (string Name, Color Value)[] GeneratorPalette = {"]
    for name in PALETTE:
        lines.append('            ("%s", %s),' % (name, cs_color(name)))
    lines += ["        };", "    }", "}"]
    path = os.path.join(ROOT, "scripts", "UI", "UIPalette.Roles.cs")
    with open(path, "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    df = build()
    write_theme(df)
    write_roles()
    print("theme: %d ext, %d sub, %d items, %d roles, %d palette entries" % (len(ext), len(subs), len(items), len(ROLES), len(PALETTE)))
