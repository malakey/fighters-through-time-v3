# Rebuilds the whole Temporal Glass UI kit from its sources (TG-ART). Run from the repository root:
#   $env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
#   powershell -NoProfile -File tools/asset_src/glass/build_all.ps1 [-LockHeld]
#
# Every Godot launch needs the repository's GdUnit lock (D:/Projects/.ftt-gdunit-lock; CLAUDE.md): this script takes
# it before its first launch (mkdir, retried every 20 s) and removes it when done, also on failure. Pass -LockHeld when
# the caller already holds it. Never launch Godot in the main checkout while the user's editor is open there.
# The chain is deterministic: a second run reproduces every output byte for byte.
param([switch]$LockHeld)
# Native exit codes are checked explicitly; "Continue" keeps PowerShell 5.1 from turning Godot's stderr warnings
# into terminating errors.
$ErrorActionPreference = "Continue"
$Lock = "D:/Projects/.ftt-gdunit-lock"
if (-not $env:GODOT_BIN) { throw "Set GODOT_BIN to the console Godot executable." }

function Run([string]$what, [scriptblock]$cmd) {
    Write-Host "== $what"
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

$took = $false
try {
    if (-not $LockHeld) {
        while ($true) {
            try { New-Item -ItemType Directory -Path $Lock -ErrorAction Stop | Out-Null; $took = $true; break }
            catch { Write-Host "GdUnit lock busy; retrying in 20 s"; Start-Sleep -Seconds 20 }
        }
    }
    Run "SVG sources and jobs"      { py tools/asset_src/glass/glass_svg.py }
    Run "rasterize (4x)"            { & $env:GODOT_BIN --headless --path . --script res://tools/generate_assets.gd -- group=glass,glass_icons_raw }
    Run "composite, gradients"      { py tools/asset_src/glass/build_glass_images.py }
    Run "select hero idle frames"   { py tools/asset_src/glass/build_select_heroes.py }
    Run "catalog"                   { py tools/asset_src/glass/write_catalog.py }
    Run "import (sidecars)"         { & $env:GODOT_BIN --headless --path . --import }
    Run "mipmaps on"                { py tools/asset_src/glass/apply_import_settings.py }
    Run "import (mipmaps)"          { & $env:GODOT_BIN --headless --path . --import }
} finally {
    if ($took) { Remove-Item -Path $Lock -Force -ErrorAction SilentlyContinue }   # the empty lock directory this run made
}
Run "contact sheets (review only)" { py tools/asset_src/glass/build_contact_sheet.py }
# Godot rewrites unrelated *.png.import sidecars with LF endings on every import: `git checkout -- <path>` them;
# commit only the glass kit's own new sidecars.
