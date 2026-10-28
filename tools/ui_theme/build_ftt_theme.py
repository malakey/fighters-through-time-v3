"""Generates resources/UI/ftt_theme.tres and scripts/UI/UIPalette.Roles.cs.

TG-THEME (Temporal Glass rebuild, 2026-10-27; first written by UR-THEME for the pulp
redesign). Run from the repository root:

    py tools/ui_theme/build_ftt_theme.py

The theme is a large, regular text resource; generating it keeps every value in
one table: the palette (UI_SPEC_GLASS section 1), the Label roles (section 2.3,
T01-T60, with the scene halos of 2.4) and the styles (sections 3.0-3.3 and 6.2).
The output is committed; edit this script, not the .tres.

The glass styles are TG-ART's nine-slices (assets/sprites/ui/glass/glass_kit.json, 6.4):
the slab and its presets, the tag, the chevron states, the tabs, the fields, the slider
grabber (TG-THEME step 2). What 6.2 builds as a StyleBoxFlat stays flat: the READY badges,
the scrim, the slider track and fill, the scroll bars, the check box ring, the tooltip.
The focus boxes are overlays (plan 0.4 P5). The five HUD bar variations are value carriers: an
empty background and a fill that holds its colour but draws nothing, under the glass tube decor
(PulpBarDecor) that draws the whole tube (TG-CLOSE, 2026-10-27, which also retired the pulp
kit's plates, roles and palette entries; the kit's legacy name "Pulp" stays on the C# types).

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
pal("DimModulate", "FFFFFF", 0.50, doc="the modulate a disabled field or tab texture draws with (agent choice)")
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


def empty(prefix="Empty"):
    return add_sub("StyleBoxEmpty", [], prefix)


def line(color, thickness):
    return add_sub("StyleBoxLine", [("color", C(color)), ("thickness", thickness)], "Line")


# ---- the glass kit (TG-ART's nine-slices, glass_kit.json) -------------------------------------
GLASS_KIT_DIR = "res://assets/sprites/ui/glass/"
_GLASS_KIT = json.load(open(os.path.join(ROOT, "assets", "sprites", "ui", "glass", "glass_kit.json"), encoding="utf-8"))
_glass_ids = {}


def glass_tex(stem):
    """An ExtResource for a glass kit texture (catalog key = its glass-prefixed stem; the file is relative to the kit)."""
    if stem not in _glass_ids:
        ext.append(("Texture2D", GLASS_KIT_DIR + _GLASS_KIT[stem]["file"], stem))
        _glass_ids[stem] = 'ExtResource("%s")' % stem
    return _glass_ids[stem]


def gbox(prefix, stem, content=None, modulate=None):
    """A StyleBoxTexture from the glass kit: texture and expand margins and the tile flag from glass_kit.json (6.4),
    content margins = the panel padding or label inset; modulate is a palette name (a dimmed state)."""
    entry = _GLASS_KIT[stem]
    p = []
    if content:
        l, t, r, b = content
        p += [("content_margin_left", float(l)), ("content_margin_top", float(t)),
              ("content_margin_right", float(r)), ("content_margin_bottom", float(b))]
    p.append(("texture", glass_tex(stem)))
    l, t, r, b = entry["nine_slice_margins_LTRB"]
    p += [("texture_margin_left", float(l)), ("texture_margin_top", float(t)),
          ("texture_margin_right", float(r)), ("texture_margin_bottom", float(b))]
    if any(entry.get("expand_margins_LTRB", [0, 0, 0, 0])):
        l, t, r, b = entry["expand_margins_LTRB"]
        p += [("expand_margin_left", float(l)), ("expand_margin_top", float(t)),
              ("expand_margin_right", float(r)), ("expand_margin_bottom", float(b))]
    if entry.get("axis_stretch") == "tile":
        p.append(("axis_stretch_horizontal", 1))
    if modulate:
        p.append(("modulate_color", C(modulate)))
    return add_sub("StyleBoxTexture", p, prefix)


FIELD_CHAMFER = 8          # 3.3: the glass field (the tooltip's flat box borrows it)


def slab(prefix, padding):
    """G1 glass slab: glass_panel (edge, build fill and drop shadow baked; the sheen and streaks are GlassSheen's).
    padding = (top, sides, bottom), the 3.1 preset, as the content margins."""
    top, sides, bottom = padding
    return gbox(prefix, "glass_panel", (sides, top, sides, bottom))


def tag_plate(prefix):
    """G2 panel tag: glass_tag (the 2 px lit bar, the gold wash, the 8 px slant), content 14 / 3 / 14 / 3 (6.4)."""
    return gbox(prefix, "glass_tag", (14, 3, 14, 3))


def chevron_set(prefix, margins):
    """G3 chevron button per state (3.2, 6.4). The focus box is TG-ART's overlay (the lit edge, the focus wash and the
    14 px glow, no dark base: plan 0.4 P5), and the pressed box is solved to compose with it into the CSS pressed look.
    hover = the normal box (orchestrator ruling): the decor takes focus on mouse-enter, so a hovered chevron draws the
    focus overlay over its normal box, exactly as a focused one; glass_btn_hover stays in the kit, unused here."""
    normal = gbox(prefix + "Normal", "glass_btn_normal", margins)
    pressed = gbox(prefix + "Pressed", "glass_btn_pressed", margins)
    return {
        "normal": normal,
        "hover": normal,
        "pressed": pressed,
        "hover_pressed": pressed,
        "disabled": gbox(prefix + "Disabled", "glass_btn_disabled", margins),
        "focus": gbox(prefix + "Focus", "glass_btn_focus", margins),
        # Not a box Godot draws by itself: the button decor swaps it in for the current value of a list (3.2 selected).
        "selected": gbox(prefix + "Selected", "glass_btn_selected", margins),
    }


def field_set(prefix, margins):
    """The glass field (3.3, plan 0.4 P8). Undecorated, so hover is TG-ART's full focused look (glass_field_hover,
    orchestrator ruling); pressed is the field itself, so under the focus overlay it reads as the focused field."""
    normal = gbox(prefix + "Normal", "glass_field", margins)
    return {
        "normal": normal,
        "hover": gbox(prefix + "Hover", "glass_field_hover", margins),
        "pressed": normal,
        "hover_pressed": normal,
        "disabled": gbox(prefix + "Disabled", "glass_field", margins, modulate="DimModulate"),
        "focus": gbox(prefix + "Focus", "glass_field_focus", margins),
    }


TAB_MARGINS = (26, 9, 26, 9)   # 3.3: padding 12 / 26, 48 tall with Outfit's natural line height


def tab_set():
    """G14 tabs: unselected, hovered (TG-ART's full look, the selected look plus the glow; orchestrator ruling),
    selected, the focus overlay (the glow only) and a dimmed disabled tab, all with the same content margins so a tab
    never changes size between states."""
    return {
        "unselected": gbox("TabUn", "glass_tab", TAB_MARGINS),
        "hovered": gbox("TabHov", "glass_tab_hover", TAB_MARGINS),
        "selected": gbox("TabSel", "glass_tab_selected", TAB_MARGINS),
        "focus": gbox("TabFocus", "glass_tab_focus", TAB_MARGINS),
        "disabled": gbox("TabDis", "glass_tab", TAB_MARGINS, modulate="DimModulate"),
    }


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


def font(face, wght, spacing=0, tnum=False, default=False, top=0, bottom=0):
    key = (face, wght, spacing, tnum, default, top, bottom)
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
    if top:
        p.append(("spacing_top", top))
    if bottom:
        p.append(("spacing_bottom", bottom))
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
    ("LevelTitle",         "cinzel", 500, 20, 0.12, "Ink",  "S", False, None, "T05 (26 until the compact Story HUD, 2026-10-27)"),
    ("ObjectiveLine",      "outfit", 400, 18, 0.0,  "Res2", "S", False, None, "T06 (22 until the compact Story HUD)"),
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
    ("HudNumeralSmall",    "outfit", 600, 20, 0.0,  "TextFocus", "S", True, None, "T17, the Story numeral (22 until the compact Story HUD)"),
    ("HudNumeralMax",      "outfit", 400, 18, 0.0,  "TextFocus", "S", True, None, "T17 / T32 /max (AC3; the widget dims it to .78 / .75)"),
    ("SlotKeyTag",         "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T18 in Story (a plain label)"),
    ("SlotKeyTagScene",    "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T18 over the scene (Fighter)"),
    ("SlotNumber",         "outfit", 700, 19, 0.0,  "TextFocus", "S", True, None, "T19 for the 54 px lens (0.36 x lens)"),
    ("StatusName",         "outfit", 500, 18, 0.16, "Ink",  "S", False, None, "T20"),
    ("StatusTime",         "outfit", 600, 21, 0.0,  "Ink2", "S", False, None, "T21 (20.7)"),
    ("DefyLabel",          "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T22 (16.2 raised to 18)"),
    ("RewindCount",        "outfit", 600, 20, 0.0,  "Ink",  None, True,  None, "T23 (32 until the compact Story HUD)"),
    ("StateLabel",         "outfit", 600, 20, 0.10, "Ink",  None, False, None, "T25 (19.8)"),
    ("DustTotal",          "outfit", 600, 28, 0.02, "Ink",  None, True,  None, "T26 (38 until the compact Story HUD)"),
    ("DustFloat",          "outfit", 600, 21, 0.02, "Res2", None, True,  None, "T27 (26 until the compact Story HUD)"),
    ("IntegrityPercent",   "outfit", 600, 18, 0.0,  "Ink",  "S", True,  None, "T28 (23 until the compact Story HUD)"),
    ("IntegrityCaption",   "outfit", 500, 18, 0.16, "Ink",  "S6", False, 1.15, "T29"),
    ("BossName",           "cinzel", 900, 26, 0.08, "TextFocus", "L", False, None, "T30 (38 until the compact Story HUD)"),
    ("BossPhaseTag",       "outfit", 500, 18, 0.16, "Ink2", "S", False, None, "T31 (a plain label)"),
    ("BossHpValue",        "outfit", 400, 20, 0.0,  "TextFocus", "S", True, None, "T32 (24 until the compact Story HUD)"),
    ("NoteLabel",          "outfit", 500, 18, 0.16, "Ink2", "S", False, 1.35, "T33"),
    ("TimerDigits",        "outfit", 600, 60, 0.04, "Ink",  None, True,  None, "T34"),
    # select
    ("CardName",           "outfit", 600, 18, 0.16, "Ink2", None, False, None, "T35 the tile name (17 raised to 18)"),
    ("CardStamp",          "outfit", 600, 26, 0.20, "Accent", None, False, None, "T37 the READY badge text"),
    ("CardQuestion",       "cinzel", 700, 40, 0.0,  "Ink2", None, False, None, "T38"),
    ("SideCardName",       "cinzel", 900, 48, 0.05, "TextFocus", "L", False, 1.08, "T40"),
    ("StatLabel",          "outfit", 500, 18, 0.16, "Ink2", None, False, None, "T41"),
    ("StatValue",          "outfit", 600, 24, 0.0,  "Ink",  None, True,  None, "T42"),
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
    # TG-CLOSE: the roles Phase B lacked (it stood in with the nearest role and a colour override)
    ("DescriptionLabel",   "outfit", 400, 24, 0.0,  "Ink2", None, False, 1.35, "9.2 the difficulty description"),
    ("BodyCompactLabel",   "outfit", 400, 24, 0.0,  "Ink",  None, False, 1.35, "9.2 body at 24 (Systems Card, dialogue log, boot, the play options notice)"),
    ("BodySmallDimLabel",  "outfit", 400, 22, 0.0,  "Ink2", None, False, None, "9.1 small copy in ink2 (the Move List stats, secondary copy)"),
    ("SlotCaption",        "outfit", 600, 18, 0.16, "Accent", None, False, None, "9.2 the Move List slot name"),
    ("LogSpeaker",         "cinzel", 700, 22, 0.13, "Accent", None, False, None, "9.2 the dialogue log's speaker"),
    ("CreditsHeading",     "cinzel", 700, 30, 0.13, "Accent", None, False, None, "9.2 the credits' role headings"),
    ("HudNumeralMaxScene", "outfit", 400, 21, 0.0,  "TextFocus", "S", True, None, "T17 /max of the Fighter numeral (20.9; HudNumeralSmall stood in)"),
    # The compact Story HUD (2026-10-27, the user's request; the Fighter HUD unchanged): the Story-only status chip text
    # (sentence case, 18 px, a trimmed line box so the name sits over the time inside the 30 px badge's row) and the Time
    # Freeze state at 18 (StateLabel stays 20 for the results panel and the select).
    ("HudStatusName",      "outfit", 500, 18, 0.01, "Ink",  "S", False, None, "compact Story HUD status name (sentence case)"),
    ("HudStatusTime",      "outfit", 600, 18, 0.0,  "Ink2", "S", False, None, "compact Story HUD status time"),
    ("HudStateLabel",      "outfit", 600, 18, 0.10, "Ink",  None, False, None, "compact Story HUD Time Freeze state"),
]

# halo tier -> (outline px, outline colour, shadow offset y, shadow outline px, shadow colour)  (2.4)
HALOS = {
    "S": (2, "HaloOutlineS", 1, 4, "HaloShadowS"),
    "S6": (2, "HaloOutlineS", 1, 6, "HaloShadowS"),
    "L": (3, "HaloOutlineL", 3, 8, "HaloShadowL"),
}


# TG-CLOSE: roles whose single line box is trimmed toward the CSS line height (FontVariation spacing_top / spacing_bottom,
# whole px): PanelTag's 22.7 px Outfit line to about 18, so the 6.4 tag plate (padding 3 + 3) stands 24 px tall.
# The compact Story HUD (2026-10-27): the status name and time trimmed to about 19 px lines, and the level title and
# objective under the slab trimmed toward the mock's line boxes, so the left cluster ends near y 260.
LINE_TRIM = {"PanelTag": (-2, -3), "HudStatusName": (-2, -2), "HudStatusTime": (-2, -2),
             "LevelTitle": (-2, -2), "ObjectiveLine": (-1, -2)}


def line_spacing(face, size, lh):
    return int(round(lh * size - NATURAL_LINE[face] * size))


def emit_label_roles():
    for (name, face, wght, size, track, color, halo, tnum, lh, _spec) in ROLES:
        put("%s/base_type" % name, '&"Label"')
        top, bottom = LINE_TRIM.get(name, (0, 0))
        put("%s/fonts/font" % name, font(face, wght, tracking_px(track, size), tnum, top=top, bottom=bottom))
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
    ("GlassPanelHud", (12, 14, 12), "the compact Story HUD slab (the Pad adds 2 on the right: 12 / 16 / 12 / 14)"),
    ("GlassPanelDust", (13, 18, 13), "the dust plate (the mock's 9 / 18 around its 42 px row; Outfit's 35 px line needs 13 to stand as tall)"),
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
    # TG-CLOSE: a compact footer that shows no prompt badge (4.2's select footer) pads its label 30 on the right.
    ("PulpButtonCompactFooter", (52, 13, 30, 13), 24, 0.13, "T11 compact footer chevron, 58 px, no badge"),
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
    # TG-CLOSE: the 3.3 arrow, chevron_down's bold weight rasterized at its 18 px display size (an OptionButton draws its
    # arrow at the texture's size); baked as ink2 / ink, so modulate_arrow (the field's font colour) lands it on ink2 at
    # rest, white when the field is lit and ink3 when it is disabled.
    put("OptionButton/icons/arrow", glass_tex("glass_option_arrow"))
    put("OptionButton/constants/modulate_arrow", 1)
    tab_font = font("outfit", 600, tracking_px(0.13, 24))
    tabs = tab_set()
    # The hover box is the quiet lit tab (TG-CLOSE: TG-ART's hover was the selected gold, which read as selected), so a
    # hovered tab's text turns white, as a lit chevron's does.
    put_button("GlassTab", "Button", {
        "normal": tabs["unselected"], "hover": tabs["hovered"], "pressed": tabs["selected"],
        "hover_pressed": tabs["selected"], "disabled": tabs["disabled"], "focus": tabs["focus"]}, 24, tab_font,
        dict(font_color="Ink2", font_hover_color="TextFocus", font_focus_color="Ink2",
             font_pressed_color="TabSelectedInk", font_hover_pressed_color="TabSelectedInk", font_disabled_color="Ink3"))
    put_button("GlassTabSelected", "Button", {
        "normal": tabs["selected"], "hover": tabs["selected"], "pressed": tabs["selected"],
        "hover_pressed": tabs["selected"], "disabled": tabs["selected"], "focus": tabs["focus"]}, 24, tab_font,
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
    # The popups share the slab at padding 8. The nine-slice's expand margins put its edge on the popup's rect, so the
    # slab reads the same whether or not the popup window clips the baked shadow outside it (not seen in a capture).
    popup_slab = slab("PopupSlab", (8, 8, 8))
    put("PopupPanel/styles/panel", popup_slab)

    def pv(name, ref):
        put(name + "/base_type", '&"PanelContainer"')
        put(name + "/styles/panel", ref)

    pv("InkPanel", default_slab)
    pv("DialogueGlassPanel", slab("DialogueSlab", DIALOGUE_PANEL_PADDING))
    for name, padding, _doc in PANEL_PRESETS:
        pv(name, slab(name, padding))
    pv("PanelTagPlate", tag_plate("Tag"))
    # TG-CLOSE: the quiet empty (disabled) slab of an empty save slot (9.2): the disabled chevron fill with the slab's
    # 18 px chamfer at top-left and bottom-right and the Continue card's padding; its dashed edge-dim edge is drawn by the
    # slab frame (MainMenu built this box locally until the closeout).
    pv("GlassPanelEmpty", flat("EmptySlab", bg="ButtonFillDisabled", radii=(18, 0, 18, 0), detail=1, margins=(20, 16, 20, 16)))
    # The dim is painted opaque; every ScrimPanel user multiplies it by SelfModulate alpha 0.70
    # (PulpScreenKit.ScrimAlpha and the three pause scenes), which gives the build dim rgba(3, 5, 14, .70) (1.4).
    pv("ScrimPanel", flat("Scrim", bg="ScrimFill", margins=(0, 0, 0, 0)))
    # G12 READY badge (6.2): its glow is a StyleBoxFlat shadow, which also fills under the translucent badge.
    pv("ReadyBadge", flat("ReadyBadge", bg="ReadyBadgeFill", border="Accent", bw=2,
                          shadow=(0, 0, 18, "ReadyBadgeGlow"), margins=(22, 8, 22, 8)))
    pv("ReadyBadgeIdle", flat("ReadyBadgeIdle", bg="ReadyBadgeIdleFill", border="ReadyBadgeIdleEdge", bw=2,
                              margins=(22, 8, 22, 8)))

    # ---- Inputs: the glass field (LineEdit), sliders, scroll bars, spin box buttons
    field_focus = field["focus"]
    line_margins = (16, 8, 16, 8)
    colors("LineEdit", font_color="Ink", font_placeholder_color="Ink3", caret_color="Accent",
           font_selected_color="Ink", selection_color="Selection")
    put("LineEdit/font_sizes/font_size", 22)
    put("LineEdit/styles/normal", gbox("LineN", "glass_field", line_margins))
    put("LineEdit/styles/focus", gbox("LineF", "glass_field_focus", line_margins))
    put("LineEdit/styles/read_only", gbox("LineR", "glass_field", line_margins, modulate="DimModulate"))
    # 3.3 slider: a 12 px track (content margins 6 and 6) leaning with a 6 px run (skew 0.5), and the glass grabber, a
    # 64 px icon (the 31.7 px diamond with its ring and glow) whose centre travels the whole track (center_grabber), as
    # the mock's handle sits at x = v %. UR-POLISH: the track's slanted right cap ends under the grabber at the maximum
    # value (expand right -6 against a 3 px lean), so it never shows past it.
    put("HSlider/styles/slider", flat("Slider", bg="SliderTrack", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, -6, 0)))
    put("HSlider/styles/grabber_area", flat("SliderFill", bg="Accent", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, 0, 0)))
    put("HSlider/styles/grabber_area_highlight", flat("SliderFillHi", bg="Focus", skew=0.5, margins=(0, 6, 0, 6), expand=(-3, 0, 0, 0)))
    put("VSlider/styles/slider", flat("VSlider", bg="SliderTrack", margins=(6, 0, 6, 0)))
    put("VSlider/styles/grabber_area", flat("VSliderFill", bg="Accent", margins=(6, 0, 6, 0)))
    put("VSlider/styles/grabber_area_highlight", flat("VSliderFillHi", bg="Focus", margins=(6, 0, 6, 0)))
    for t in ("HSlider", "VSlider"):
        for icon in ("grabber", "grabber_highlight", "grabber_disabled"):
            put(t + "/icons/" + icon, glass_tex("glass_slider_grabber"))
        put(t + "/constants/center_grabber", 1)
        # TG-CLOSE: a focused slider wears the glass field's focus overlay (the lit edge and the 14 px glow; Settings
        # borrowed it per slider until the closeout folded it here). The grabber canvas is 44 px, so a slider is 44 tall.
        put(t + "/styles/focus", field_focus)
    # TG-CLOSE: a focusable Label (the select's Remote Play notice) wears the same overlay, not Godot's default frame.
    put("Label/styles/focus", field_focus)
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
    # TG-CLOSE polish (6.2: "option button, spin box ... arrows = the chevron_down icon"): the up and down buttons draw the
    # kit's 18 px chevrons in every state, the down one the OptionButton arrow and the up one its vertical flip (both
    # baked ink2 / ink), tinted like the field's text so they land on ink2 at rest, bright when lit, dim when disabled.
    for d, stem in (("up", "glass_spin_up"), ("down", "glass_option_arrow")):
        for state in ("", "_hover", "_pressed", "_disabled"):
            put("SpinBox/icons/%s%s" % (d, state), glass_tex(stem))
        colors("SpinBox", **{d + "_icon_modulate": "Ink", d + "_hover_icon_modulate": "TextFocus",
                             d + "_pressed_icon_modulate": "TextFocus", d + "_disabled_icon_modulate": "Ink3"})

    # ---- Progress bars. The default ProgressBar wears the glass tube's dark track and gold edge (an undecorated bar,
    # such as the boot notice's timer). The five HUD bar variations are value carriers under the glass tube decor
    # (PulpBarDecor, GAUGE-A, which draws the whole tube): an empty background and a fill that holds its colour
    # (FighterHUD.TintFill recolours it, the decor reads it) but draws nothing, so the bar draws nothing of its own
    # (plan 2.2; TG-CLOSE, which also retired the tube decor's hide-self material).
    colors("ProgressBar", font_color="Ink")
    put("ProgressBar/font_sizes/font_size", 18)
    put("ProgressBar/styles/background", flat("ProgBg", bg="TubeTrackTop", border="Edge", bw=2))
    put("ProgressBar/styles/fill", flat("ProgFill", bg="Accent"))
    for name, fill_color, font_color, size in (("PulpBar", "Hp", "Ink", 22), ("PulpBarH44", "Hp", "Ink", 22),
                                               ("PulpBarH16", "Meter2", "Ink", 22), ("PulpBarH14", "Accent", "Ink", 22),
                                               ("PulpBossBar", "Danger", "TextFocus", 24)):
        put("%s/base_type" % name, '&"ProgressBar"')
        put("%s/styles/background" % name, empty("BarNone"))
        put("%s/styles/fill" % name, flat(name + "Fill", bg=fill_color, center=False))
        colors(name, font_color=font_color)
        put("%s/font_sizes/font_size" % name, size)

    # ---- Tabs (G14): TabContainer's own styles; the inner panel is a quiet field that keeps the pulp content
    # margins (the Settings layout is TG-PAUSE's).
    put("TabContainer/styles/panel", flat("TabPanel", bg="TabPanelFill", border="EdgeDim", bw=2, radii=(12, 0, 12, 0),
                                          detail=1, margins=(18, 18, 24, 26)))
    put("TabContainer/styles/tab_selected", tabs["selected"])
    put("TabContainer/styles/tab_unselected", tabs["unselected"])
    put("TabContainer/styles/tab_hovered", tabs["hovered"])
    put("TabContainer/styles/tab_disabled", tabs["disabled"])
    put("TabContainer/styles/tab_focus", tabs["focus"])
    put("TabContainer/styles/tabbar_background", empty("TabBar"))
    colors("TabContainer", font_selected_color="TabSelectedInk", font_hovered_color="TextFocus",
           font_unselected_color="Ink2", font_disabled_color="Ink3")
    put("TabContainer/font_sizes/font_size", 24)
    put("TabContainer/fonts/font", tab_font)

    # ---- Popup menu (a glass slab of 44 px rows), tooltip (a small flat slab: a tooltip is often shorter than the
    # glass_panel nine-slice's corners), separators
    put("PopupMenu/styles/panel", popup_slab)
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
    ("PulpButtonCompactFooter", "Button, T11: the compact chevron of a footer with no prompt badge, label padded 30 on the right (4.2; legacy name)"),
    ("TemporalGlassButton", "Button, T10: the pause and list chevron row (64 px)"),
    ("GlassField", "Button: the glass field (3.3, chamfer 8), the same as the default Button"),
    ("GlassTab", "Button: a G14 tab (pressed = selected) for a tab row built from buttons"),
    ("GlassTabSelected", "Button: a G14 tab in its selected look"),
    ("InkPanel", "PanelContainer: the glass slab G1, default padding 22 / 28 / 22 (legacy name)"),
    ("DialogueGlassPanel", "PanelContainer: the glass slab G1, dialogue padding 22 / 26 / 22"),
    ("GlassPanelEmpty", "PanelContainer: the quiet empty (disabled) slab, the Continue card's padding (an empty save slot; 9.2)"),
] + [(name, "PanelContainer: the glass slab G1 for %s, padding %d / %d / %d" % ((doc,) + padding))
     for name, padding, doc in PANEL_PRESETS] + [
    ("PanelTagPlate", "PanelContainer: the glass panel tag G2"),
    ("ScrimPanel", "PanelContainer: the pause dim, painted opaque; every user sets SelfModulate alpha 0.70"),
    ("ReadyBadge", "PanelContainer: the G12 READY badge"),
    ("ReadyBadgeIdle", "PanelContainer: the G12 badge while choosing"),
    ("PulpBar", "ProgressBar: the Story HP value carrier, 30 px, under the glass tube decor (draws nothing itself; legacy name)"),
    ("PulpBarH44", "ProgressBar: the Fighter HP value carrier, 44 px, under the glass tube decor"),
    ("PulpBarH16", "ProgressBar: the Fighter Ultimate value carrier, 16 px, under the glass tube decor"),
    ("PulpBarH14", "ProgressBar: the select stat value carrier, 14 px, under the glass tube decor"),
    ("PulpBossBar", "ProgressBar: the boss value carrier, 34 px, under the glass tube decor"),
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
