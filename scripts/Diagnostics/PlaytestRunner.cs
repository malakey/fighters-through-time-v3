using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using FTT.Core;
using FTT.UI;

namespace FTT.Diagnostics {

    /// <summary>
    /// Entry scene for an automated Story playtest. Run it headless:
    ///
    /// <code>
    /// Godot_console --headless --fixed-fps 60 --path &lt;repo&gt; res://scenes/diagnostics/PlaytestRunner.tscn -- \
    ///     --level=2 --character=joan --bot=spam --difficulty=normal --max-seconds=300 --out=user://playtest/run.json
    /// </code>
    ///
    /// It starts the level through the developer direct-launch path
    /// (<see cref="StoryManager.StartLevelDirect"/>: slotless, so no save is ever
    /// written and the full kit is available), installs a <see cref="PlaytestBot"/>
    /// on player 0, records <see cref="PlaytestTelemetry"/>, writes a JSON report,
    /// prints a single <c>PLAYTEST_RESULT</c> line and quits.
    ///
    /// <para>The scene root is replaced by the level load, so the work lives on a
    /// <see cref="PlaytestSession"/> node parented to the tree root, where it
    /// survives <c>ChangeSceneToPacked</c> the same way the loading screen does.</para>
    /// </summary>
    public partial class PlaytestRunner : Node {
        public override void _Ready() {
            // Automated runs must never write the player's real saves.
            SaveManager.SuppressWritesForDiagnostics = true;
            PlaytestOptions options;
            try {
                options = PlaytestOptions.Parse(OS.GetCmdlineUserArgs());
            } catch (Exception exception) {
                GD.PushError($"PLAYTEST bad arguments: {exception.Message}");
                GetTree().Quit(2);
                return;
            }
            var session = new PlaytestSession { Name = "PlaytestSession", Options = options };
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, session);
        }
    }

    public sealed class PlaytestOptions {
        public CampaignLevel Level = CampaignLevel.Orleans;
        public string Character = "joan";
        public string Bot = "spam";
        public Difficulty Difficulty = Difficulty.Normal;
        /// <summary>Run length cap in seconds; 0 = the level's Integrity budget (par x difficulty) + 60.</summary>
        public int MaxSeconds = 0;
        public string OutPath = "";
        /// <summary>Enemy the feel probe spawns in front of the hero.</summary>
        public string Enemy = "chrono_slasher";
        /// <summary>Save PNG frames while the bot asks for capture (needs a windowed, non-headless run).</summary>
        public bool Capture;
        /// <summary>Captured frames are downscaled to this width (height keeps aspect).</summary>
        public int CaptureWidth = 960;
        /// <summary>Crop width around the hero in viewport units (0 = full frame).</summary>
        public int CaptureCropWidth = 520;
        /// <summary>Free-form suffix appended to output names so repeated runs do not overwrite each other.</summary>
        public string Tag = "";
        /// <summary>
        /// Apply the Legacy Unlock Schedule a real campaign would have at this
        /// level (the slotless dev launch otherwise grants the full kit, so a
        /// Level 2 run would use Specials a player only unlocks after Level 3).
        /// Default on, except for the feel probe, which exercises the whole kit.
        /// </summary>
        public bool? CampaignKit;
        /// <summary>Write the navigation graph (segments + edges) to user://playtest/nav_*.txt at start and finish.</summary>
        public bool NavDump;
        /// <summary>Trace sample interval in frames (default 60 = once a second).</summary>
        public int TraceEvery = 60;
        /// <summary>
        /// Sample index. Runs are otherwise frame-identical replays (enemy RNG is
        /// seeded by spawn order), so a seed holds neutral input for a few
        /// seed-derived frames at the start, shifting every later bot timing
        /// against the world. Seed 0 = no offset (the classic run).
        /// </summary>
        public int Seed;

        public static PlaytestOptions Parse(string[] args) {
            var options = new PlaytestOptions();
            foreach (string raw in args) {
                string arg = raw.TrimStart('-');
                int split = arg.IndexOf('=');
                if (split < 0) continue;
                string key = arg[..split].ToLowerInvariant();
                string value = arg[(split + 1)..];
                switch (key) {
                    case "level": options.Level = (CampaignLevel)int.Parse(value); break;
                    case "character": options.Character = value.ToLowerInvariant(); break;
                    case "bot": options.Bot = value.ToLowerInvariant(); break;
                    case "difficulty": options.Difficulty = Enum.Parse<Difficulty>(value, ignoreCase: true); break;
                    case "max-seconds": options.MaxSeconds = int.Parse(value); break;
                    case "out": options.OutPath = value; break;
                    case "enemy": options.Enemy = value; break;
                    case "capture": options.Capture = value != "0" && value != "false"; break;
                    case "capture-width": options.CaptureWidth = int.Parse(value); break;
                    case "capture-crop": options.CaptureCropWidth = int.Parse(value); break;
                    case "tag": options.Tag = value; break;
                    case "campaign-kit": options.CampaignKit = value != "0" && value != "false"; break;
                    case "nav-dump": options.NavDump = value != "0" && value != "false"; break;
                    case "trace-every": options.TraceEvery = Math.Max(1, int.Parse(value)); break;
                    case "debug-ropes": FTT.Diagnostics.NavAgent.DebugRopes = value != "0" && value != "false"; break;
                    case "seed": options.Seed = int.Parse(value); break;
                }
            }
            if (string.IsNullOrEmpty(options.OutPath)) {
                options.OutPath = $"user://playtest/{options.RunName}.json";
            }
            return options;
        }

        public string RunName =>
            $"L{(int)Level:00}_{Character}_{Bot}_{Difficulty.ToString().ToLowerInvariant()}{(string.IsNullOrEmpty(Tag) ? "" : "_" + Tag)}{(Seed == 0 ? "" : "_s" + Seed)}";

        /// <summary>Neutral-input frames before the bot takes control (0 for seed 0; 5..60 otherwise).</summary>
        public int SeedDelayFrames => Seed == 0 ? 0 : 5 + (int)((uint)(Seed * 2654435761u) % 56u);
    }

    /// <summary>Holds neutral input for <c>delay</c> ticks, then hands every sample to the bot.</summary>
    public sealed class DelayedStartSource : IPlayerInputSource {
        private readonly IPlayerInputSource _inner;
        private int _remaining;

        public DelayedStartSource(IPlayerInputSource inner, int delay) {
            _inner = inner;
            _remaining = delay;
        }

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) {
            if (_remaining > 0) {
                _remaining--;
                return PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None, previousFrame.Held);
            }
            return _inner.Sample(tick, previousFrame);
        }
    }

    public partial class PlaytestSession : Node {
        /// <summary>Unpaused-frame limit on waiting for the level to appear.</summary>
        private const int LoadTimeoutFrames = 60 * 60;

        public PlaytestOptions Options;

        private string _levelPath = "";
        private PlaytestWorld _world;
        private PlaytestBot _bot;
        private PlaytestTelemetry _telemetry;
        private bool _running;
        private bool _finished;
        private int _waitFrames;
        private int _dialogueAdvanceCooldown;
        private int _dialogueLinesAdvanced;
        private string _lastDialogueID = "";
        private string _endReason = "";

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            _levelPath = StoryManager.GetLevelScenePath(Options.Level);
            GD.Print($"PLAYTEST start level={(int)Options.Level} ({_levelPath}) character={Options.Character} bot={Options.Bot} difficulty={Options.Difficulty}");
            if (StoryManager.Instance == null) {
                Finish("no_story_manager");
                return;
            }
            StoryManager.Instance.StartLevelDirect(Options.Level, Options.Character, Options.Difficulty);
        }

        public override void _PhysicsProcess(double delta) {
            if (_finished) return;
            SceneTree tree = GetTree();

            if (!_running) {
                if (tree.CurrentScene != null && tree.CurrentScene.SceneFilePath == _levelPath) {
                    BeginRun(tree);
                } else if (++_waitFrames > LoadTimeoutFrames) {
                    Finish("level_never_loaded");
                }
                return;
            }

            AdvanceDialogue();
            AdvanceScriptedBeats();
            if (tree.Paused) {
                // A paused tree with no dialogue to advance and no scripted beat
                // running is a softlock (a modal nobody answers): report it.
                if (++_pausedFrames > PausedWatchdogFrames) {
                    Finish($"stuck_paused:{DescribePause(tree)}");
                }
                return;
            }
            _pausedFrames = 0;

            ApplyCampaignKitOnce();
            _world.Tick();
            _telemetry.Tick(tree);
            RouteInteract();
            if (_bot.NavNotes.Count > 0) {
                foreach (string note in _bot.NavNotes) _telemetry.Note("nav", note);
                _bot.NavNotes.Clear();
            }
            if (Options.NavDump && _telemetry.Frame == 30) WriteNavDump("start");
            if (_bot is FeelProbeBot probe) {
                _feelLog ??= new FeelLog();
                _feelLog.Record(probe, _world.Player);
                _captureActive = Options.Capture && probe.CaptureActive;
                if (probe.Done) {
                    Finish("feel_probe_done");
                    return;
                }
            }

            // Out of the world: the hero is far below every standable surface
            // and still falling. Levels author no kill boundary (VERIFY-STORY-PITS),
            // so without this a geometry hole is an endless fall.
            if (_world.Player is FTT.Characters.PlayerController hero) {
                float lowest = _world.Nav.LowestTop;
                if (!float.IsNegativeInfinity(lowest) && hero.GlobalPosition.Y > lowest + 700f) {
                    if (++_outOfWorldFrames > 90) {
                        Finish($"fell_out_of_world@{Mathf.RoundToInt(hero.GlobalPosition.X)},{Mathf.RoundToInt(lowest)}");
                        return;
                    }
                } else {
                    _outOfWorldFrames = 0;
                }
            }

            if (_telemetry.LevelCompleted) Finish("level_complete");
            else if (tree.CurrentScene == null || tree.CurrentScene.SceneFilePath != _levelPath) {
                string destination = tree.CurrentScene?.SceneFilePath ?? "none";
                // A Timeline Collapse extracts to the hub: say why.
                string cause = destination.Contains("HubWorld") || destination.Contains("GameOver")
                    ? $"collapse({StoryManager.Instance?.PendingCollapseCause.ToString() ?? "?"}):" : "";
                Finish($"left_level:{cause}{destination}");
            } else if (_telemetry.Frame >= _maxFrames) Finish("timeout");
        }

        // === Watchdogs and scripted beats ====================================
        private const int PausedWatchdogFrames = 60 * 30;
        private int _pausedFrames;
        private int _maxFrames = int.MaxValue;

        /// <summary>
        /// The Level 9 near-capture pauses the tree for a struggle the player
        /// wins by holding away from the Eraser. A bot cannot press raw keys, so
        /// the session pulls away through the beat's own public seam.
        /// </summary>
        private void AdvanceScriptedBeats() {
            if (_world == null) return;
            foreach (FTT.Environment.NearCaptureBeat beat in _world.NearCaptures) {
                if (!GodotObject.IsInstanceValid(beat) || beat.Phase != FTT.Environment.NearCaptureBeat.NearCapturePhase.Struggle) continue;
                if (beat.AdvanceStruggle(1f / 60f, 1f)) _telemetry?.Note("near_capture", beat.ToreFreeByInput ? "tore_free" : "auto_release");
            }
        }

        private static string DescribePause(SceneTree tree) {
            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue != null && dialogue.IsSequenceActive) return "dialogue";
            return tree.CurrentScene?.Name ?? "unknown";
        }

        private void BeginRun(SceneTree tree) {
            _world = new PlaytestWorld(tree);
            _bot = PlaytestBotFactory.Create(Options.Bot, _world, Options.Level, Options.Enemy);
            int seconds = Options.MaxSeconds;
            if (seconds <= 0) {
                // Auto: the level's Timeline Integrity budget on this difficulty
                // (par x 2.0 / 1.5 / 1.2) plus a minute for the post-boss beats.
                float par = tree.CurrentScene is FTT.Environment.StoryLevelControllerBase level ? level.ParSeconds : 360f;
                float multiplier = Options.Difficulty switch { Difficulty.Easy => 2f, Difficulty.Hard => 1.2f, _ => 1.5f };
                seconds = Mathf.CeilToInt(Mathf.Max(par, 300f) * multiplier) + 60;
            }
            _maxFrames = seconds * 60;
            _telemetry = new PlaytestTelemetry { TraceEveryFrames = Options.TraceEvery };
            _telemetry.Attach(_world, _bot);
            InputManager.Instance?.SetInputSource(0, Options.SeedDelayFrames > 0
                ? new DelayedStartSource(_bot, Options.SeedDelayFrames)
                : _bot);
            if (Options.Capture) RenderingServer.FramePostDraw += OnFramePostDraw;
            _running = true;
        }

        // === Frame capture (windowed runs only) =============================
        private int _outOfWorldFrames;
        private bool _kitApplied;
        private string _kitDescription = "full";

        private void ApplyCampaignKitOnce() {
            if (_kitApplied) return;
            bool campaignKit = Options.CampaignKit ?? Options.Bot != "feel";
            if (!campaignKit) {
                _kitApplied = true;
                return;
            }
            if (_world.Player is not FTT.Characters.PlayerController hero) return;
            var unlocked = new System.Collections.Generic.List<AbilitySlot>();
            foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
                // A slot restored "after Level N" is available from Level N+1 on.
                if (LegacyUnlockSchedule.MilestoneLevelFor(slot) < (int)Options.Level) unlocked.Add(slot);
            }
            hero.ApplyLegacyUnlockLocks(unlocked);
            _kitDescription = unlocked.Count == LegacyUnlockSchedule.GatedSlots.Count ? "full" : string.Join("+", unlocked);
            _kitApplied = true;
        }

        private int _lastRoutedSample = -1;
        public int InteractionsRouted { get; private set; }

        /// <summary>
        /// <c>InteractionArea</c> hears Interact only as a raw Godot input event
        /// (<c>_UnhandledInput</c>), never through <see cref="InputManager"/>
        /// frames. Each bot Interact press is therefore delivered the way that
        /// handler would: <see cref="FTT.Environment.InteractionArea.TryInteract"/>
        /// on an area the hero overlaps — the one the bot aimed at, else the
        /// first in range. The Warden Beacon and the puzzle reset station are
        /// never pressed blind (they open menus / undo a puzzle). Held channels
        /// (Resonance Hold, Restoration Font) read <c>CurrentInputFrame</c> and
        /// need nothing. No raw input is injected, so dialogue hold-to-skip
        /// never sees a phantom held Interact.
        /// </summary>
        private void RouteInteract() {
            if (_bot == null || _bot.SampleCount == _lastRoutedSample) return;
            _lastRoutedSample = _bot.SampleCount;
            if ((_bot.LastPressed & GameplayButtons.Interact) == 0) return;
            if (_world.Player is not FTT.Characters.PlayerController hero) return;
            FTT.Environment.InteractionArea aimed = PlaytestWorld.InteractionAreaOf(_bot.InteractTarget);
            if (aimed != null && aimed.OverlapsBody(hero) && aimed.TryInteract(hero)) {
                InteractionsRouted++;
                _telemetry?.Note("interact", _bot.InteractTarget?.Name ?? "");
                return;
            }
            if (_bot.InteractTarget != null) return;
            Godot.Collections.Array<Node> areas = GetTree().CurrentScene?.FindChildren("*", nameof(FTT.Environment.InteractionArea), true, false);
            if (areas == null) return;
            using var lifetime = areas.AsDisposable();
            foreach (Node node in areas) {
                if (node is not FTT.Environment.InteractionArea area || !area.OverlapsBody(hero)) continue;
                Node owner = area.GetParent();
                if (owner is FTT.Environment.WardenBeacon or FTT.Environment.PuzzleResetStation) continue;
                if (area.TryInteract(hero)) {
                    InteractionsRouted++;
                    _telemetry?.Note("interact", owner?.Name ?? "");
                    return;
                }
            }
        }
        private FeelLog _feelLog;
        private bool _captureActive;
        private int _capturedFrames;

        private void OnFramePostDraw() {
            if (!_captureActive || _finished || _telemetry == null) return;
            Image image = GetViewport()?.GetTexture()?.GetImage();
            if (image == null || image.IsEmpty()) return;
            if (Options.CaptureCropWidth > 0 && _world?.Player is FTT.Characters.PlayerController player) {
                // Crop around the hero: the camera frames a whole room, which
                // leaves a fighter ~30 px tall in a full frame.
                Vector2 visible = GetViewport().GetVisibleRect().Size;
                float scale = image.GetWidth() / Mathf.Max(1f, visible.X);
                Vector2 feet = player.GetGlobalTransformWithCanvas().Origin * scale;
                int cropW = Mathf.RoundToInt(Options.CaptureCropWidth * scale);
                int cropH = Mathf.RoundToInt(Options.CaptureCropWidth * 0.6f * scale);
                int x = Mathf.Clamp(Mathf.RoundToInt(feet.X - cropW / 2f), 0, Mathf.Max(0, image.GetWidth() - cropW));
                int y = Mathf.Clamp(Mathf.RoundToInt(feet.Y - cropH * 0.72f), 0, Mathf.Max(0, image.GetHeight() - cropH));
                image = image.GetRegion(new Rect2I(x, y, Mathf.Min(cropW, image.GetWidth()), Mathf.Min(cropH, image.GetHeight())));
            }
            if (Options.CaptureWidth > 0 && image.GetWidth() > Options.CaptureWidth) {
                int height = Mathf.RoundToInt(image.GetHeight() * (Options.CaptureWidth / (float)image.GetWidth()));
                image.Resize(Options.CaptureWidth, height, Image.Interpolation.Bilinear);
            }
            string directory = ProjectSettings.GlobalizePath($"user://playtest/frames/{Options.RunName}");
            System.IO.Directory.CreateDirectory(directory);
            image.SavePng(System.IO.Path.Combine(directory, $"f_{_telemetry.Frame:00000}.png"));
            _capturedFrames++;
        }

        /// <summary>
        /// Dialogue reads raw Godot input, which a bot cannot press, so the session
        /// advances lines itself through the public API. Skipping still resolves a
        /// sequence's gameplay effects, exactly like a player who reads quickly.
        /// </summary>
        private void AdvanceDialogue() {
            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue == null || !dialogue.IsSequenceActive) {
                _lastDialogueID = "";
                return;
            }
            // Every sequence that plays, in order (EventBus.OnDialogueTriggered
            // is not raised for level-scripted sequences, so poll the manager).
            string active = dialogue.ActiveDialogueID;
            if (active != _lastDialogueID) {
                _lastDialogueID = active;
                if (!string.IsNullOrEmpty(active)) _telemetry.Note("dialogue", active);
            }
            if (--_dialogueAdvanceCooldown > 0) return;
            _dialogueAdvanceCooldown = 6;
            dialogue.AdvanceLine();
            _dialogueLinesAdvanced++;
        }

        private void Finish(string reason) {
            if (_finished) return;
            _finished = true;
            _endReason = reason;
            InputManager.Instance?.ClearInputSource(0);
            if (Options.NavDump && _world != null) WriteNavDump("end");
            _telemetry?.Detach();
            _world?.Detach();
            if (Options.Capture) RenderingServer.FramePostDraw -= OnFramePostDraw;
            if (_feelLog != null) {
                string feelPath = ProjectSettings.GlobalizePath($"user://playtest/feel_{Options.RunName}.csv");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(feelPath));
                System.IO.File.WriteAllText(feelPath, _feelLog.Csv);
                GD.Print($"PLAYTEST feel log {feelPath} ({_capturedFrames} frames captured)");
            }

            var report = new Dictionary<string, object> {
                ["level"] = (int)Options.Level,
                ["level_path"] = _levelPath,
                ["character"] = Options.Character,
                ["bot"] = Options.Bot,
                ["difficulty"] = Options.Difficulty.ToString(),
                ["end_reason"] = _endReason,
                ["kit"] = _kitDescription,
                ["interactions"] = InteractionsRouted,
                ["nav_rebuilds"] = _world?.Nav.Rebuilds ?? 0,
                ["dialogue_lines_skipped"] = _dialogueLinesAdvanced
            };
            if (_telemetry != null) {
                foreach (KeyValuePair<string, object> entry in _telemetry.ToReport()) report[entry.Key] = entry.Value;
            }

            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            WriteReport(json);

            var summary = new Dictionary<string, object> {
                ["level"] = report["level"], ["character"] = report["character"], ["bot"] = report["bot"],
                ["difficulty"] = report["difficulty"], ["end_reason"] = _endReason,
                ["seconds"] = report.GetValueOrDefault("seconds"),
                ["progress_px"] = report.GetValueOrDefault("progress_px"),
                ["checkpoints"] = report.GetValueOrDefault("checkpoints_first_activations"),
                ["boss_defeated"] = (_telemetry?.BossDefeatedFrame ?? -1) >= 0,
                ["player"] = report.GetValueOrDefault("player"),
                ["enemy_summary"] = report.GetValueOrDefault("enemy_summary"),
                ["report"] = ProjectSettings.GlobalizePath(Options.OutPath)
            };
            GD.Print("PLAYTEST_RESULT " + JsonSerializer.Serialize(summary));
            GetTree().Paused = false;
            GetTree().Quit(0);
        }

        private void WriteNavDump(string when) {
            string path = ProjectSettings.GlobalizePath($"user://playtest/nav_{Options.RunName}_{when}.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, _world.Nav.Dump());
        }

        private void WriteReport(string json) {
            string absolute = ProjectSettings.GlobalizePath(Options.OutPath);
            string directory = System.IO.Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(absolute, json);
        }
    }
}
