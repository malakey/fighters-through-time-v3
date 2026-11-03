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

    Bots: bypass (fights only what the run requires: the members of the
    encounter lock it stands in, the boss, a blocker in an escape; never
    defends, breaks only what blocks it - since the 2026-10-12 encounter locks
    it is a lock-regression probe, not a run-past-everything probe), spam
    (mashed 3-hit string + Special 1 + Special 2 as soon as ready, never
    blocks), basics (string only, blocks enemy swings), play (a competent
    player: string + specials + Ultimate, blocks, avoids projectiles and
    hazards). Every bot navigates with the nav graph, walks into each encounter
    lock and clears it, strikes the checkpoints no lock holds, solves the
    level's puzzles and seals the level. A lock that makes no progress for 60 s
    with no member the bot can reach (120 s regardless) ends the run
    stuck_lock:<lockID>. feel is the frame-exact feel probe (see
    PlaytestFeel.cs and analyze_feel.py).

    -MaxSeconds 0 sizes each run to the level's Timeline Integrity budget on
    the chosen difficulty: the par the clock runs on (authored par x the
    provisional par scale 1.5, TimelineIntegrityRules.ProvisionalParScale, at
    least 300 s) x 2.0 / 1.5 / 1.2, plus a minute.

    The CSV carries the encounter-lock and crowd columns (since 2026-10-12):
    locks / locks_cleared / lock_reverts / left_lock_alive (the hero left an
    uncleared lock's area with members alive) / lock_relocations, and
    atk_max / atk_mean (mobs attacking within 250 px of the hero, over the
    engaged frames), melee_max, melee_over_slots_f (frames with more melee
    attackers near the hero than the difficulty's slot total, slot_total),
    handoffs (slot hand-offs), waiter_gap_med and ranged_max. Since FIX-BOT
    (2026-10-13) locks and locks_cleared count distinct lock IDs, and
    lock_reloads counts the lock instances a scene reload (an Act III Anchor
    Snap) rebuilt.

    Since CM-BOT (2026-10-16) the CSV also carries xsrc_rehits (the hero's
    damaging hits taken within 30 frames of a hit from a different source -
    the mercy-invulnerability measurement, decision 7.2; the report's rehits
    block has the rest, including the hit count without status ticks) and
    hits_taken, every HP loss the hero took (player.hits_taken). Since
    CM-FIXA-PRES (2026-10-16) xsrc_rehits counts hits only - every periodic
    tick (status, zone pulse, drown tick, stage-hazard DPS) is neither a
    re-hit nor a predecessor - and xsrc_rehits_combat is the same figure
    with hazard contacts left out on both sides.

    Since MM-BOTS (2026-10-31, the meter and Defy update) the CSV also carries
    defies (Defies the hero executed; only the play bot presses Defy),
    defy_died3s (executions after which the hero died within 3 s), starved
    (Special presses refused for want of a Momentum pip), momentum0 (the gauge
    at level start, 2.0 on a fresh level) and specials_per_min. The report's
    defy and momentum blocks have the rest. Batches from before and after that
    update are NOT comparable (the gauge, the 0.5 Story meter scale, the
    active Defy): compare within one side of it.

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
    # D6d (2026-10-11): the combat summary; absent from older runners.
    $c = $x.combat
    # GU-BOT (2026-10-12): the encounter-lock and crowd summary; absent from older runners.
    $n = $x.encounters
    $rows += [pscustomobject]@{
        level = $x.level; character = $x.character; bot = $x.bot; difficulty = $x.difficulty
        end = ($x.end_reason -replace 'res://scenes/campaign/', '' -replace '\.tscn', '')
        seconds = $x.seconds; progress_px = $x.progress_px
        checkpoints = $x.checkpoints; boss_down = $x.boss_defeated
        dmg_taken = $x.player.damage_taken; deaths = $x.player.deaths
        seen = $e.seen; killed = $e.killed; passed_alive = $e.passed_while_alive; passed_hit = $e.passed_then_hit_player
        gave_up = $e.bot_gave_up_unreachable
        median_ttk_f = $e.median_frames_to_kill; ttk_le_2s = $e.killed_within_2s_of_first_hit; one_burst = $e.killed_by_one_burst
        whiff_pct = $(if ($c) { $c.whiff_pct } else { $null })
        ults = $(if ($c) { $c.ults } else { $null })
        s_refused = $(if ($c) { $c.s_refused } else { $null })
        meter0 = $(if ($c) { $c.meter_start } else { $null })
        # MM-BOTS (2026-10-31): the active Defy and the Momentum gauge (absent before the update).
        defies = $(if ($c) { $c.defies } else { $null })
        defy_died3s = $(if ($c) { $c.defy_died3s } else { $null })
        starved = $(if ($c) { $c.starved } else { $null })
        momentum0 = $(if ($c) { $c.momentum0 } else { $null })
        specials_per_min = $(if ($c) { $c.specials_per_min } else { $null })
        # C3 (2026-10-11): mob melee whiff % and the Special press policy (--special-press).
        mob_whiff_pct = $(if ($c) { $c.mob_whiff_pct } else { $null })
        policy = $(if ($c) { $c.policy } else { $null })
        # C2 (2026-10-11): the run's D5 Story meter scale (--story-meter-scale).
        meter_scale = $(if ($c) { $c.meter_scale } else { $null })
        # CM-BOT (2026-10-16): hero hits taken within 30 frames of another
        # source's hit (absent before CM-BOT), and every HP loss the hero took
        # (status ticks included; the report's rehits.hits excludes them).
        xsrc_rehits = $(if ($c) { $c.xsrc_rehits } else { $null })
        # CM-FIXA-PRES (2026-10-16): the same with hazard contacts left out on
        # both sides (absent before the fix).
        xsrc_rehits_combat = $(if ($c) { $c.xsrc_rehits_combat } else { $null })
        hits_taken = $x.player.hits_taken
        # GU-BOT (2026-10-12): encounter locks and the hero's crowd.
        locks = $(if ($n) { $n.locks } else { $null })
        locks_cleared = $(if ($n) { $n.locks_cleared } else { $null })
        lock_reloads = $(if ($n) { $n.lock_reloads } else { $null })
        lock_reverts = $(if ($n) { $n.lock_reverts } else { $null })
        left_lock_alive = $(if ($n) { $n.left_lock_alive } else { $null })
        lock_relocations = $(if ($n) { $n.lock_relocations } else { $null })
        atk_max = $(if ($n) { $n.atk_max } else { $null })
        atk_mean = $(if ($n) { $n.atk_mean } else { $null })
        melee_max = $(if ($n) { $n.melee_max } else { $null })
        melee_over_slots_f = $(if ($n) { $n.melee_over_slots_f } else { $null })
        slot_total = $(if ($n) { $n.slot_total } else { $null })
        handoffs = $(if ($n) { $n.handoffs } else { $null })
        waiter_gap_med = $(if ($n) { $n.waiter_gap_med } else { $null })
        ranged_max = $(if ($n) { $n.ranged_max } else { $null })
        # GR-CROWD (2026-11-03): a free slot with a ringing candidate, the melee supply, swarm waves met.
        slot_idle_f = $(if ($n) { $n.slot_idle_f } else { $null })
        melee_cand_mean = $(if ($n) { $n.melee_cand_mean } else { $null })
        swarms = $(if ($n) { $n.swarms } else { $null })
    }
}

$rows = $rows | Sort-Object level, character, bot
$rows | Format-Table -AutoSize | Out-String -Width 400
$csv = Join-Path $outDir "summary_$Difficulty$(if ($Tag) { "_$Tag" }).csv"
$rows | Export-Csv -NoTypeInformation -Path $csv
Write-Host "Reports: $outDir"
Write-Host "Summary: $csv"
