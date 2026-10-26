"""Generates resources/UI/ftt_theme.tres and scripts/UI/UIPalette.Roles.cs.

UR-THEME (pulp UI redesign, 2026-10-26). Run from the repository root:

    py tools/ui_theme/build_ftt_theme.py

The theme is a large, regular text resource; generating it keeps every
variation's numbers in one table (UI_SPEC section 2 roles T01-T50 and section
3.0 plates). The output is committed; edit this script, not the .tres.
Hex values below mirror scripts/UI/UIPalette.cs (UIThemeTests pins the two).
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


# ---- palette (PAL-B) -------------------------------------------------------
def col(hexstr, a=1.0):
    hexstr = hexstr.lstrip("#")
    r, g, b = (int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return "Color(%.6g, %.6g, %.6g, %.6g)" % (r, g, b, a)


PLATE = ("090C1E", 0.80)
INK, INK2, INK3 = "F3F0E6", "C0BAA6", "8A8374"
ACCENT, ACCENT2 = "F3C95B", "FFE39A"
PAPER = "F3F0E6"
BLACK = "000000"
LABEL_DARK = "0C0A10"
TRACK = "3A3144"
SLOT = "2A2434"
DISABLED_LABEL = "A0987F"
DISABLED_BORDER = "6B6150"
DISABLED_FILL = "2C2733"
DANGER = "FF5A5A"
HP = "8CF0B8"
PAPER_INK, PAPER_INK2 = "14101A", "3A3144"
RES2 = "FFF0B0"
PROMPT = "FFF3D8"
P1, P2 = "00F0FF", "FF3366"
BG2 = ("1E1C34", 0.78)


def C(c):
    return col(*c) if isinstance(c, tuple) else col(c)


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


def flat(prefix, bg=None, border=None, bw=0, radius=0, skew=0.0, shadow=None,
         margins=None, center=True, expand=0, bw_sides=None):
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
        p.append(("border_color", C(border or BLACK)))
    if radius:
        p += [("corner_radius_top_left", radius), ("corner_radius_top_right", radius),
              ("corner_radius_bottom_right", radius), ("corner_radius_bottom_left", radius)]
    if expand:
        p += [("expand_margin_left", float(expand)), ("expand_margin_top", float(expand)),
              ("expand_margin_right", float(expand)), ("expand_margin_bottom", float(expand))]
    if shadow:
        dx, dy, size, scol = shadow
        p += [("shadow_color", C(scol)), ("shadow_size", size),
              ("shadow_offset", "Vector2(%g, %g)" % (dx, dy))]
    return add_sub("StyleBoxFlat", p, prefix)


KIT_DIR = "res://assets/sprites/ui/pulp/"
_KIT = json.load(open(os.path.join(ROOT, "assets", "sprites", "ui", "pulp", "pulp_kit.json"), encoding="utf-8"))
_tex_ids = {}


def kit_tex(stem):
    """An ExtResource for a kit PNG (catalog key = file stem)."""
    if stem not in _tex_ids:
        ident = "kit_" + stem
        ext.append(("Texture2D", KIT_DIR + _KIT[stem]["file"], ident))
        _tex_ids[stem] = 'ExtResource("%s")' % ident
    return _tex_ids[stem]


def tbox(prefix, stem, content=None):
    """A StyleBoxTexture from the UR-ART kit: margins and expand margins come from pulp_kit.json."""
    entry = _KIT[stem]
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


def HARD(dx, dy=None, size=1):
    return (dx, dx if dy is None else dy, size, BLACK)


# ---- fonts -----------------------------------------------------------------
CINZEL = add_ext("FontFile", "res://assets/fonts/cinzel/Cinzel-Variable.ttf", "font_cinzel")
OUTFIT = add_ext("FontFile", "res://assets/fonts/outfit/Outfit-Variable.ttf", "font_outfit")
WGHT, TNUM = 2003265652, 1953063277   # OpenType tags 'wght' and 'tnum' as ints
CHECK_ON = add_ext("Texture2D", "res://assets/ui/theme_placeholder/check_on.png", "tex_check_on")
CHECK_OFF = add_ext("Texture2D", "res://assets/ui/theme_placeholder/check_off.png", "tex_check_off")
RADIO_ON = add_ext("Texture2D", "res://assets/ui/theme_placeholder/radio_on.png", "tex_radio_on")
RADIO_OFF = add_ext("Texture2D", "res://assets/ui/theme_placeholder/radio_off.png", "tex_radio_off")

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


# ---- theme item collection --------------------------------------------------
items = []    # (key, value)


def put(key, value):
    items.append((key, value))


def colors(type_, **kv):
    for k, v in kv.items():
        put("%s/colors/%s" % (type_, k), C(v))


# ---- Label type roles (UI_SPEC 2.2) ------------------------------------------
# name, face, wght, px, tracking em, colour, outline px, shadow (dx, dy) or None, tnum, spec
ROLES = [
    # the existing four, re-pointed (sizes honour the 18 px floor of plan item 14)
    ("SmallLabel",       "outfit", 500, 18, 0.0,  INK2, 0, None, False, "micro 18 px"),
    ("HeadingLabel",     "cinzel", 700, 26, 0.06, INK,  0, None, False, "heading, Cinzel 700"),
    ("EmphasisLabel",    "outfit", 600, 30, 0.0,  INK,  0, None, False, "emphasis, Outfit 600"),
    ("TitleLabel",       "cinzel", 700, 40, 0.08, ACCENT, 0, None, False, "screen title, Cinzel 700"),
    # cover and banners
    ("CoverTitle",       "cinzel", 500, 170, 0.01, ACCENT, 7, (2, 2), False, "T01"),
    ("CoverBanner",      "outfit", 700, 70, 0.10, PAPER, 3, None, False, "T02"),
    ("BannerHeading",    "cinzel", 500, 58, 0.03, LABEL_DARK, 0, None, False, "T03"),
    ("PauseTitle",       "cinzel", 900, 78, 0.14, "FFFFFF", 0, (0, 4), False, "T04"),
    ("LevelTitle",       "cinzel", 500, 26, 0.12, INK, 0, (0, 2), False, "T05"),
    ("ObjectiveLine",    "outfit", 400, 22, 0.0,  RES2, 0, (0, 2), False, "T06"),
    ("PanelTag",         "outfit", 600, 21, 0.16, LABEL_DARK, 0, None, False, "T07"),
    ("HeroName",         "cinzel", 700, 22, 0.13, INK, 0, None, False, "T08"),
    ("HeroNameScene",    "cinzel", 700, 28, 0.13, INK, 3, None, False, "T09"),
    ("CaptionLabel",     "outfit", 500, 18, 0.16, INK2, 0, None, False, "T14, T15, T48"),
    ("SceneCaption",     "outfit", 500, 18, 0.16, INK, 3, None, False, "T24, T29, scene captions"),
    ("StatusLine",       "outfit", 400, 18, 0.06, INK2, 0, None, False, "T16"),
    ("PromptCaption",    "outfit", 500, 18, 0.16, PROMPT, 2, None, False, "T13"),
    ("PromptKey",        "outfit", 700, 20, 0.0,  LABEL_DARK, 0, None, False, "T12"),
    ("HintLine",         "outfit", 500, 22, 0.0,  PROMPT, 3, None, False, "T44"),
    # HUD
    ("HudNumeral",       "outfit", 400, 30, 0.02, INK, 3, None, True,  "T17, T26, T28 numerals"),
    ("HudNumeralSmall",  "outfit", 400, 22, 0.02, INK, 3, None, True,  "T17 at bar height 30"),
    ("SlotKeyTag",       "outfit", 700, 18, 0.06, LABEL_DARK, 0, None, False, "T18 (15 raised to the 18 px floor)"),
    ("SlotNumber",       "outfit", 400, 20, 0.0,  "FFFFFF", 2, (0, 2), True, "T19"),
    ("StatusName",       "outfit", 500, 18, 0.16, INK, 0, None, False, "T20"),
    ("StatusTime",       "outfit", 600, 21, 0.0,  INK2, 0, None, False, "T21"),
    ("DefyLabel",        "outfit", 500, 18, 0.16, INK2, 0, None, False, "T22 (16.2 raised to 18)"),
    ("RewindCount",      "outfit", 600, 32, 0.0,  INK, 3, None, False, "T23"),
    ("StateLabel",       "outfit", 600, 20, 0.10, INK, 3, None, False, "T25"),
    ("DustFloat",        "outfit", 400, 26, 0.02, RES2, 2, None, False, "T27"),
    ("BossName",         "cinzel", 900, 40, 0.08, PAPER, 3, (0, 3), False, "T30"),
    ("BossPhaseTag",     "outfit", 700, 18, 0.16, LABEL_DARK, 0, None, False, "T31"),
    ("BossHpValue",      "outfit", 400, 24, 0.0,  "FFFFFF", 3, (0, 1), True, "T32"),
    ("NoteLabel",        "outfit", 500, 18, 0.16, INK2, 0, (0, 2), False, "T33"),
    ("TimerDigits",      "outfit", 600, 60, 0.04, INK, 0, None, True,  "T34"),
    # cards and select
    ("CardName",         "outfit", 700, 25, 0.06, PAPER, 0, None, False, "T35"),
    ("CardDates",        "outfit", 500, 18, 0.12, ACCENT, 0, None, False, "T36 (17 raised to 18)"),
    ("CardStamp",        "outfit", 400, 36, 0.06, PAPER, 0, None, False, "T37"),
    ("CardQuestion",     "outfit", 400, 110, 0.0, ACCENT, 3, None, False, "T38"),
    ("BubbleText",       "outfit", 400, 46, 0.04, LABEL_DARK, 0, None, False, "T39"),
    ("SideCardName",     "cinzel", 900, 31, 0.05, LABEL_DARK, 0, None, False, "T40"),
    ("StatLabel",        "outfit", 500, 18, 0.16, PAPER_INK2, 0, None, False, "T41"),
    ("StatValue",        "outfit", 600, 24, 0.0,  PAPER_INK, 0, None, False, "T42"),
    ("StarBadgeText",    "outfit", 400, 44, 0.0,  LABEL_DARK, 0, None, False, "T43"),
    ("StickerLine1",     "outfit", 400, 46, 0.04, LABEL_DARK, 0, None, False, "T45"),
    ("StickerLine2",     "outfit", 700, 30, 0.08, "7A1F19", 0, None, False, "T46"),
    ("StickerLine3",     "outfit", 600, 20, 0.06, LABEL_DARK, 0, None, False, "T47"),
    # dialogue
    ("DialogueBody",     "outfit", 500, 33, 0.0,  INK, 0, None, False, "T49"),
    ("SpeakerName",      "cinzel", 900, 30, 0.10, ACCENT, 0, None, False, "T50"),
]


def emit_label_roles():
    for (name, face, wght, size, track, color, outline, shadow, tnum, _spec) in ROLES:
        spacing = int(round(track * size))
        put("%s/base_type" % name, '&"Label"')
        put("%s/fonts/font" % name, font(face, wght, spacing, tnum))
        put("%s/font_sizes/font_size" % name, size)
        put("%s/colors/font_color" % name, C(color))
        if outline:
            put("%s/colors/font_outline_color" % name, C(BLACK))
            put("%s/constants/outline_size" % name, outline)
        if shadow:
            put("%s/colors/font_shadow_color" % name, col(BLACK, 0.8))
            put("%s/constants/shadow_offset_x" % name, shadow[0])
            put("%s/constants/shadow_offset_y" % name, shadow[1])


def button_set(prefix, margins, height=58, skew=0.1405):
    """normal / hover = focus / pressed / disabled / focus from the kit's plates at the given control height
    (hover = focus per the spec; the disabled stripes tile)."""
    h = "_h%d" % height
    return {
        "normal": tbox(prefix + "Normal", "btn_normal" + h, margins),
        "hover": tbox(prefix + "Hover", "btn_focus" + h, margins),
        "pressed": tbox(prefix + "Pressed", "btn_pressed" + h, margins),
        "disabled": tbox(prefix + "Disabled", "btn_disabled" + h, margins),
        "focus": tbox(prefix + "Focus", "btn_focus" + h, margins),
    }


def button_set_flat(prefix, margins, skew=0.1405):
    """normal / hover / pressed / disabled / focus boxes for a pulp button (UI_SPEC 3.0 rows 4-8, flat)."""
    return {
        "normal": flat(prefix + "Normal", PLATE, PAPER, 3, 0, skew, HARD(7), margins),
        "hover": flat(prefix + "Hover", ACCENT, BLACK, 3, 0, skew, HARD(11), margins),
        "pressed": flat(prefix + "Pressed", ACCENT2, BLACK, 3, 0, skew, None, margins),
        "disabled": flat(prefix + "Disabled", DISABLED_FILL, DISABLED_BORDER, 3, 0, skew, HARD(4), margins),
        "focus": flat(prefix + "Focus", ACCENT, BLACK, 3, 0, skew, HARD(11), margins),
    }


def put_button(type_, base, boxes, size, font_ref):
    if base:
        put("%s/base_type" % type_, '&"%s"' % base)
    for state, ref in boxes.items():
        put("%s/styles/%s" % (type_, state), ref)
    colors(type_, font_color=INK, font_hover_color=LABEL_DARK, font_focus_color=LABEL_DARK,
           font_pressed_color=LABEL_DARK, font_hover_pressed_color=LABEL_DARK,
           font_disabled_color=DISABLED_LABEL)
    put("%s/font_sizes/font_size" % type_, size)
    put("%s/fonts/font" % type_, font_ref)


def build():
    default_font = font("outfit", 400, default=True)

    # ---- Buttons
    plain = button_set("Btn", (24, 8, 26, 8), 58)
    put_button("Button", None, plain, 20, font("outfit", 700, 1))
    put_button("PulpButton", "Button", button_set("PulpBtn", (64, 18, 34, 19), 80), 34, font("outfit", 700, 2))
    put_button("PulpButtonCompact", "Button", button_set("PulpBtnSm", (54, 11, 24, 12), 58), 27, font("outfit", 700, 2))
    put_button("TemporalGlassButton", "Button", button_set("GlassBtn", (42, 15, 34, 16), 64), 26, font("outfit", 700, 2))
    put_button("OptionButton", None, plain, 20, font("outfit", 700, 1))
    put("OptionButton/constants/arrow_margin", 8)

    # CheckBox: an accent focus ring, placeholder icons.
    ring = flat("Ring", None, ACCENT, 3, 0, 0, None, None, center=False, expand=3)
    put("CheckBox/styles/focus", ring)
    for state in ("normal", "hover", "pressed", "hover_pressed", "disabled"):
        put("CheckBox/styles/" + state, empty("CheckBoxNone"))
    colors("CheckBox", font_color=INK, font_hover_color=ACCENT, font_focus_color=ACCENT,
           font_pressed_color=ACCENT, font_hover_pressed_color=ACCENT,
           font_disabled_color=DISABLED_LABEL)
    put("CheckBox/font_sizes/font_size", 20)
    put("CheckBox/constants/h_separation", 12)
    for name, tex in (("checked", CHECK_ON), ("unchecked", CHECK_OFF),
                      ("radio_checked", RADIO_ON), ("radio_unchecked", RADIO_OFF),
                      ("checked_disabled", CHECK_ON), ("unchecked_disabled", CHECK_OFF),
                      ("radio_checked_disabled", RADIO_ON), ("radio_unchecked_disabled", RADIO_OFF)):
        put("CheckBox/icons/" + name, tex)

    # ---- Labels
    colors("Label", font_color=INK)
    put("Label/font_sizes/font_size", 20)
    emit_label_roles()

    # ---- Panels
    dark = tbox("Panel", "panel_dark", (22, 18, 28, 28))
    put("Panel/styles/panel", dark)
    put("PanelContainer/styles/panel", dark)
    popup = flat("Popup", PLATE, PAPER, 3, 0, 0, None, (10, 10, 10, 10))
    put("PopupPanel/styles/panel", popup)

    def pv(name, ref):
        put(name + "/base_type", '&"PanelContainer"')
        put(name + "/styles/panel", ref)

    pv("InkPanel", dark)
    pv("PaperPanel", tbox("Paper", "panel_paper", (22, 18, 28, 28)))
    pv("HudPlatePanel", tbox("Hud", "panel_dark", (26, 20, 32, 30)))
    pv("DialogueGlassPanel", tbox("Glass", "panel_dark", (28, 20, 34, 30)))
    pv("BannerPanel", tbox("Banner", "banner_select", (64, 8, 70, 10)))
    pv("PanelTagPlate", tbox("Tag", "tag_panel", (14, 6, 18, 8)))
    pv("BossNameplate", tbox("BossName", "boss_nameplate", (26, 6, 30, 10)))
    pv("BossPhaseTab", tbox("BossPhase", "boss_phase_tab", (14, 5, 16, 6)))
    pv("SlotPlate", tbox("Slot", "slot_frame", (6, 6, 8, 8)))
    pv("SlotPlateReady", tbox("SlotReady", "slot_frame_ready", (6, 6, 8, 8)))
    pv("KeyTagPlate", tbox("KeyTag", "key_tag", (7, 3, 7, 2)))
    pv("CardPlate", tbox("Card", "card_frame", (10, 10, 14, 14)))
    pv("CardPlateP1", tbox("CardP1", "card_frame_p1", (10, 10, 14, 14)))
    pv("CardPlateP2", tbox("CardP2", "card_frame_p2", (10, 10, 14, 14)))
    pv("BubblePlate", tbox("Bubble", "bubble", (26, 10, 32, 14)))
    pv("StampPlate", tbox("Stamp", "stamp_ready", (16, 6, 20, 8)))
    pv("ScrimPanel", flat("Scrim", ("020308", 0.85), None, 0, 0, 0, None, (0, 0, 0, 0)))

    # ---- Inputs
    colors("LineEdit", font_color=INK, font_placeholder_color=DISABLED_LABEL,
           caret_color=ACCENT, font_selected_color=LABEL_DARK)
    put("LineEdit/colors/selection_color", col(ACCENT, 0.85))
    put("LineEdit/font_sizes/font_size", 20)
    put("LineEdit/styles/normal", flat("LineN", PLATE, INK3, 2, 0, 0, None, (12, 8, 12, 8)))
    put("LineEdit/styles/focus", flat("LineF", PLATE, ACCENT, 3, 0, 0, None, (12, 8, 12, 8)))
    put("LineEdit/styles/read_only", flat("LineR", DISABLED_FILL, DISABLED_BORDER, 2, 0, 0, None, (12, 8, 12, 8)))
    track = tbox("Slider", "slider_track", (0, 5, 0, 5))
    for t in ("HSlider", "VSlider"):
        put(t + "/styles/slider", track)
        put(t + "/styles/grabber_area", tbox("GrabArea", "slider_fill"))
        put(t + "/styles/grabber_area_highlight", tbox("GrabAreaHi", "slider_fill"))
        for icon in ("grabber", "grabber_highlight", "grabber_disabled"):
            put(t + "/icons/" + icon, kit_tex("slider_grabber"))
    for t in ("VScrollBar", "HScrollBar"):
        put(t + "/styles/scroll", flat("Scroll", TRACK, BLACK, 2, 0))
        put(t + "/styles/scroll_focus", flat("ScrollF", TRACK, ACCENT, 2, 0))
        put(t + "/styles/grabber", flat("Grab", INK2, BLACK, 2, 0))
        put(t + "/styles/grabber_highlight", flat("GrabHi", ACCENT, BLACK, 2, 0))
        put(t + "/styles/grabber_pressed", flat("GrabPr", ACCENT2, BLACK, 2, 0))
    for state, fill in (("", PLATE), ("hover_", ACCENT), ("pressed_", ACCENT2), ("disabled_", DISABLED_FILL)):
        for d in ("up", "down"):
            put("SpinBox/styles/%s_%sbackground" % (d, state), flat("Spin", fill, BLACK, 2, 0))

    # ---- Progress bars (the fill stays a StyleBoxFlat: FighterHUD.TintFill recolours it)
    colors("ProgressBar", font_color=INK)
    put("ProgressBar/font_sizes/font_size", 18)
    put("ProgressBar/styles/background", flat("ProgBg", TRACK, BLACK, 3, 0))
    put("ProgressBar/styles/fill", flat("ProgFill", ACCENT, None, 0, 0))
    put("PulpBar/base_type", '&"ProgressBar"')
    put("PulpBar/styles/background", tbox("BarBg", "bar_track_h30", (3, 3, 3, 3)))
    put("PulpBar/styles/fill", flat("BarFill", HP, None, 0, 0, 0.2126))
    colors("PulpBar", font_color=INK)
    put("PulpBar/font_sizes/font_size", 22)
    put("PulpBossBar/base_type", '&"ProgressBar"')
    put("PulpBossBar/styles/background", tbox("BossBarBg", "bar_track_boss", (5, 5, 5, 5)))
    put("PulpBossBar/styles/fill", flat("BossBarFill", DANGER, None, 0, 0, 0.1763))
    colors("PulpBossBar", font_color="FFFFFF")
    put("PulpBossBar/font_sizes/font_size", 24)

    # ---- Tabs
    put("TabContainer/styles/panel", tbox("TabPanel", "panel_dark", (18, 18, 24, 26)))
    put("TabContainer/styles/tab_selected", tbox("TabSel", "tab_selected", (18, 8, 22, 10)))
    put("TabContainer/styles/tab_unselected", tbox("TabUn", "tab_normal", (18, 8, 22, 10)))
    put("TabContainer/styles/tab_hovered", tbox("TabHov", "tab_selected", (18, 8, 22, 10)))
    put("TabContainer/styles/tab_focus", ring)
    put("TabContainer/styles/tabbar_background", empty("TabBar"))
    colors("TabContainer", font_selected_color=LABEL_DARK, font_hovered_color=LABEL_DARK,
           font_unselected_color=INK)
    put("TabContainer/font_sizes/font_size", 20)
    put("TabContainer/fonts/font", font("outfit", 700, 1))

    # ---- Popup menu, tooltip, separators
    put("PopupMenu/styles/panel", popup)
    put("PopupMenu/styles/hover", flat("MenuHover", ACCENT, None, 0, 0, 0, None, (8, 4, 8, 4)))
    put("PopupMenu/styles/separator", line(INK3, 2))
    colors("PopupMenu", font_color=INK, font_hover_color=LABEL_DARK,
           font_disabled_color=DISABLED_LABEL, font_separator_color=INK3)
    put("PopupMenu/font_sizes/font_size", 20)
    put("TooltipPanel/styles/panel", flat("Tip", PLATE, PAPER, 2, 0, 0, HARD(4), (10, 6, 10, 6)))
    colors("TooltipLabel", font_color=INK)
    put("TooltipLabel/font_sizes/font_size", 18)
    put("HSeparator/styles/separator", line(INK3, 3))
    put("VSeparator/styles/separator", line(INK3, 3))
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
    ("PulpButton", "Button, T10: menu / pause / footer banner button"),
    ("PulpButtonCompact", "Button, T11: compact banner button"),
    ("TemporalGlassButton", "Button, legacy name kept: the pulp plate at 26 px"),
    ("InkPanel", "PanelContainer, plate 1: dark ink panel"),
    ("PaperPanel", "PanelContainer, plate 2: paper panel"),
    ("HudPlatePanel", "PanelContainer: HUD plate"),
    ("DialogueGlassPanel", "PanelContainer, legacy name kept: the dialogue box ink plate"),
    ("BannerPanel", "PanelContainer, plate 24: gold banner"),
    ("PanelTagPlate", "PanelContainer, plate 3: panel tag"),
    ("BossNameplate", "PanelContainer, plate 11: boss nameplate"),
    ("BossPhaseTab", "PanelContainer, plate 12: boss PHASE tab"),
    ("SlotPlate", "PanelContainer, plate 13: ability slot"),
    ("SlotPlateReady", "PanelContainer, plate 14: ready slot"),
    ("KeyTagPlate", "PanelContainer, plate 16: slot key tag"),
    ("CardPlate", "PanelContainer, plate 20: trading card"),
    ("CardPlateP1", "PanelContainer, plate 21: P1 pick"),
    ("CardPlateP2", "PanelContainer, plate 22: P2 pick"),
    ("BubblePlate", "PanelContainer, plate 27: speech bubble"),
    ("StampPlate", "PanelContainer, plate 26: card stamp"),
    ("ScrimPanel", "PanelContainer: translucent scrim"),
    ("PulpBar", "ProgressBar, plate 9: skewed HP / meter track (the fill stays a StyleBoxFlat)"),
    ("PulpBossBar", "ProgressBar, plate 10: boss bar"),
]


def write_roles():
    lines = ["// <auto-generated> by tools/ui_theme/build_ftt_theme.py - edit the script, not this file.",
             "namespace FTT.UI {",
             "    public static partial class UIPalette {",
             "        /// <summary>Label type-variation names authored in the shared theme (UI_SPEC 2.2 roles).</summary>",
             "        public static class LabelRoles {"]
    for (name, face, wght, size, track, color, outline, shadow, tnum, spec) in ROLES:
        lines.append("            /// <summary>%s %d, %d px, %.2f em, outline %d (%s).</summary>"
                     % (face.capitalize(), wght, size, track, outline, spec))
        lines.append('            public const string %s = "%s";' % (name, name))
    lines += ["        }", "",
              "        /// <summary>Every Label role: authored font size, pinned face and weight (UIThemeTests).</summary>",
              "        public static readonly (string Name, int Size, string Face, int Weight)[] LabelRoleTable = {"]
    for (name, face, wght, size, *_rest) in ROLES:
        lines.append('            ("%s", %d, "%s", %d),' % (name, size, face, wght))
    lines += ["        };", "",
              "        /// <summary>Non-Label variations authored in the shared theme.</summary>",
              "        public static class Variations {"]
    for name, doc in VARIATIONS:
        lines.append("            /// <summary>%s.</summary>" % doc)
        lines.append('            public const string %s = "%s";' % (name, name))
    lines += ["        }", "    }", "}"]
    path = os.path.join(ROOT, "scripts", "UI", "UIPalette.Roles.cs")
    with open(path, "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    df = build()
    write_theme(df)
    write_roles()
    print("theme: %d ext, %d sub, %d items" % (len(ext), len(subs), len(items)))
