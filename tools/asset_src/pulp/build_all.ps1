# Rebuilds the whole pulp UI kit from its sources (UR-ART).  Run from the repository root.
# Every Godot launch needs the repository's GdUnit lock (D:/Projects/.ftt-gdunit-lock); acquire it first.
#   $env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
$ErrorActionPreference = "Stop"
py tools/asset_src/pulp/build_pulp_svgs.py                   # SVG sources, jobs.json, catalog_svg.json
& $env:GODOT_BIN --headless --path . --script res://tools/generate_assets.gd -- group=pulp,pulp_icons_raw
py tools/asset_src/pulp/build_pulp_images.py                 # cover art, rays, halftone, split, card field, .tres
py tools/asset_src/pulp/finish_pulp_icons.py                 # fill / ink / sh icon layers, star badge
py tools/asset_src/pulp/build_ability_icons.py               # 144 px ability icons from the VFX atlases
py tools/asset_src/pulp/build_cover_heroes.py                # cover hero poses
py tools/asset_src/pulp/write_catalog.py                     # pulp_kit.json
& $env:GODOT_BIN --headless --path . --import
py tools/asset_src/pulp/apply_import_settings.py             # mipmaps on the scaled / tilted assets
& $env:GODOT_BIN --headless --path . --import
py tools/asset_src/pulp/build_contact_sheet.py               # D:/Projects/ftt-scratch/ur-captures/art/
py tools/asset_src/pulp/build_kit_demo.py
