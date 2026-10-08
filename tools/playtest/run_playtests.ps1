<#
.SYNOPSIS
    Runs scripted Story playtest bots headlessly and prints a comparison table.

.DESCRIPTION
    Each combination of level x character x bot launches
    res://scenes/diagnostics/PlaytestRunner.tscn in its own headless Godot
    process (--fixed-fps 60, so a run is faster than real time and every frame
    is exactly 1/60 s). Up to -Parallel processes run at once. Each run writes a
    full JSON report to user://playtest/ and prints one PLAYTEST_RESULT line,
    which this script collects into summary.csv beside the reports.

    Bots: bypass (runs past everything, breaks only what blocks it, fights only
    the boss), spam (mashed 3-hit string + Special 1 + Special 2 as soon as
    ready, never blocks), basics (string only, blocks enemy swings), play (a
    competent player: string + specials + Ultimate, blocks, avoids projectiles
    and hazards). Every bot navigates with the nav graph, strikes checkpoints,
    solves the level's puzzles and seals the level. feel is the frame-exact
    feel probe (see PlaytestFeel.cs and analyze_feel.py).

    -MaxSeconds 0 sizes each run to the level's Timeline Integrity budget on
    the chosen difficulty (par x 2.0 / 1.5 / 1.2, plus a minute).

    The runner uses the developer direct-launch path and suppresses save
    writes, so no save slot or global save is touched. Build first:
    dotnet build FightersThroughTime.csproj

    Inspect a run's once-a-second trace with:
    python tools/playtest/trace.py L02_joan_play_normal_<tag>

.EXAMPLE
    ./tools/playtest/run_playtests.ps1 -Levels 2,5 -Characters joan,lincoln -Bots spam,basics,bypass,play -Parallel 6
#>
param(
    [int[]]$Levels = @(2),
    [string[]]$Characters = @("joan"),
    [string[]]$Bots = @("bypass", "spam", "basics", "play"),
    [ValidateSet("easy", "normal", "hard")][string]$Difficulty = "normal",
    [int]$MaxSeconds = 600,
    [int]$Parallel = 6,
    [string]$Tag = "",
    # Extra runner arguments, e.g. "--trace-every=20","--nav-dump=1"
    [string[]]$ExtraArgs = @(),
    [string]$GodotBin = $(if ($env:GODOT_BIN) { $env:GODOT_BIN } else { "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" })
)

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$outDir = Join-Path $env:APPDATA "Godot\app_userdata\Fighters Through Time\playtest"
$logDir = Join-Path $outDir "logs"
New-Item -ItemType Directory -Force $logDir | Out-Null

$jobs = @()
foreach ($level in $Levels) { foreach ($character in $Characters) { foreach ($bot in $Bots) {
    $jobs += [pscustomobject]@{ Level = $level; Character = $character; Bot = $bot }
} } }

$running = @()
$done = @()
$queue = [System.Collections.Queue]::new($jobs)
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $Parallel) {
        $j = $queue.Dequeue()
        $name = "L$($j.Level)_$($j.Character)_$($j.Bot)_$Difficulty$(if ($Tag) { "_$Tag" })"
        $log = Join-Path $logDir "$name.log"
        $argList = @("--headless", "--fixed-fps", "60", "--path", "`"$repo`"", "res://scenes/diagnostics/PlaytestRunner.tscn", "--",
            "--level=$($j.Level)", "--character=$($j.Character)", "--bot=$($j.Bot)", "--difficulty=$Difficulty",
            "--max-seconds=$MaxSeconds")
        if ($Tag) { $argList += "--tag=$Tag" }
        if ($ExtraArgs) { $argList += $ExtraArgs }
        $p = Start-Process -FilePath $GodotBin -ArgumentList $argList -RedirectStandardOutput $log `
            -RedirectStandardError "$log.err" -NoNewWindow -PassThru
        $running += [pscustomobject]@{ Proc = $p; Job = $j; Log = $log; Name = $name }
        Write-Host "start $name" -ForegroundColor DarkGray
    }
    Start-Sleep -Milliseconds 500
    $still = @()
    foreach ($r in $running) {
        if ($r.Proc.HasExited) { $done += $r; Write-Host "done  $($r.Name)" -ForegroundColor Cyan } else { $still += $r }
    }
    $running = $still
}

$rows = @()
foreach ($r in $done) {
    $result = Get-Content $r.Log -ErrorAction SilentlyContinue | Where-Object { "$_" -like "PLAYTEST_RESULT *" } | Select-Object -Last 1
    if (-not $result) {
        Write-Warning "No PLAYTEST_RESULT for $($r.Name) (see $($r.Log))"
        continue
    }
    $x = ("$result".Substring("PLAYTEST_RESULT ".Length)) | ConvertFrom-Json
    $e = $x.enemy_summary
    $rows += [pscustomobject]@{
        level = $x.level; character = $x.character; bot = $x.bot; difficulty = $x.difficulty
        end = ($x.end_reason -replace 'res://scenes/campaign/', '' -replace '\.tscn', '')
        seconds = $x.seconds; progress_px = $x.progress_px
        checkpoints = $x.checkpoints; boss_down = $x.boss_defeated
        dmg_taken = $x.player.damage_taken; deaths = $x.player.deaths
        seen = $e.seen; killed = $e.killed; passed_alive = $e.passed_while_alive; passed_hit = $e.passed_then_hit_player
        gave_up = $e.bot_gave_up_unreachable
        median_ttk_f = $e.median_frames_to_kill; ttk_le_2s = $e.killed_within_2s_of_first_hit; one_burst = $e.killed_by_one_burst
    }
}

$rows = $rows | Sort-Object level, character, bot
$rows | Format-Table -AutoSize | Out-String -Width 250
$csv = Join-Path $outDir "summary_$Difficulty$(if ($Tag) { "_$Tag" }).csv"
$rows | Export-Csv -NoTypeInformation -Path $csv
Write-Host "Reports: $outDir"
Write-Host "Summary: $csv"
