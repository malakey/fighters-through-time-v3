using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using Godot;

namespace FTT.Diagnostics {
    /// <summary>
    /// Render one real opening at 1920x1080, then exit. Run with a rendering backend, without --headless.
    /// After -- pass --story=0..15 OR --fighter=stage_id, --hero=einstein, --opponent=joan,
    /// --settle=12, --out=directory; optional --hide-ui affects only this diagnostic capture.
    /// --story-x=worldX [--story-y=worldY] relocates and freezes the hero's physics, then waits
    /// --relocate-settle=180 physics frames for camera/scale handoff. This is not traversal proof.
    /// --fighter-platform=zeroBasedIndex freezes only the fighter simulation driver and relocates P1
    /// to the selected canonical platform; P2 stays at its starting point. Uses the same settle option.
    /// Writes target_hero.png and target_hero.json. Exit 0 succeeds, 2 indicates failure.
    /// </summary>
    public partial class LevelOpeningCaptureRunner : Node {
        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            _ = CaptureAsync();
        }

        private async Task CaptureAsync() {
            try {
                var arguments = new Dictionary<string, string>();
                foreach (string argument in OS.GetCmdlineUserArgs()) {
                    int equals = argument.IndexOf('=');
                    if (argument.StartsWith("--") && equals > 2)
                        arguments[argument.Substring(2, equals - 2)] = argument.Substring(equals + 1);
                    else if (argument == "--hide-ui") arguments["hide-ui"] = "true";
                    else throw new ArgumentException($"Unknown argument: {argument}");
                }
                bool story = arguments.TryGetValue("story", out string storyText);
                bool fighter = arguments.TryGetValue("fighter", out string fighterID);
                if (story == fighter) throw new ArgumentException("Pass exactly one of --story=0..15 or --fighter=stage_id.");
                string hero = arguments.GetValueOrDefault("hero", "einstein");
                string opponent = arguments.GetValueOrDefault("opponent", "joan");
                if (!CharacterRoster.Contains(hero) || !CharacterRoster.Contains(opponent))
                    throw new ArgumentException("Hero and opponent must be roster IDs.");
                string path;
                string target;
                if (story) {
                    if (!int.TryParse(storyText, out int index) || index < 0 || index > 15)
                        throw new ArgumentException("Story index must be 0..15.");
                    path = StoryManager.GetLevelScenePath((CampaignLevel)index);
                    target = $"story_{index:00}";
                } else {
                    FighterStageData stage = FighterStageCatalog.LoadDefault()?.Find(fighterID);
                    if (stage == null) throw new ArgumentException($"Unknown fighter stage: {fighterID}");
                    path = stage.ScenePath;
                    target = $"fighter_{fighterID}";
                }
                if (GameManager.Instance == null) throw new InvalidOperationException("GameManager autoload is unavailable.");
                GameManager.Instance.CurrentSession.SelectedCharacterID = hero;
                GameManager.Instance.CurrentSession.OpponentCharacterID = opponent;
                GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
                GameManager.Instance.CurrentSession.SelectedStageID = fighterID ?? "";
                int settle = int.Parse(arguments.GetValueOrDefault("settle", "12"));
                if (settle < 1 || settle > 600) throw new ArgumentException("Settle frames must be 1..600.");
                bool relocate = arguments.TryGetValue("story-x", out string relocationText);
                if (relocate && !story) throw new ArgumentException("--story-x requires --story.");
                bool platformRelocate = arguments.TryGetValue("fighter-platform", out string platformText);
                if (platformRelocate && !fighter) throw new ArgumentException("--fighter-platform requires --fighter.");
                int platformIndex = platformRelocate ? int.Parse(platformText, CultureInfo.InvariantCulture) : -1;
                if (arguments.ContainsKey("resume"))
                    throw new ArgumentException("Resume capture is unavailable: the production resume path requires a writable save slot. This diagnostic never activates one.");
                float relocationX = relocate ? ParseCoordinate(relocationText, "story-x") : 0f;
                int relocationSettle = int.Parse(arguments.GetValueOrDefault("relocate-settle", "180"));
                if (relocationSettle < 1 || relocationSettle > 600)
                    throw new ArgumentException("Relocate settle frames must be 1..600.");
                var viewport = new SubViewport {
                    Name = "CaptureViewport", Size = new Vector2I(1920, 1080),
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                    HandleInputLocally = true, GuiEmbedSubwindows = true, World2D = new World2D()
                };
                AddChild(viewport);
                Node shown = ResourceLoader.Load<PackedScene>(path)?.Instantiate()
                    ?? throw new InvalidOperationException($"Could not instantiate {path}");
                viewport.AddChild(shown);
                for (int frame = 0; frame < settle; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (arguments.ContainsKey("hide-ui")) {
                    foreach (Node node in Descendants(shown)) {
                        if (node is CanvasLayer layer) layer.Hide();
                        // Keep diagnostic framing live under an entrance dialogue's pause without
                        // completing dialogue, applying its effects, or changing persisted state.
                        if (node is Camera2D || node is StoryOpeningPresentation)
                            node.ProcessMode = ProcessModeEnum.Always;
                    }
                }
                if (relocate) {
                    PlayerController player = null;
                    foreach (Node node in Descendants(shown)) {
                        if (node is PlayerController candidate) { player = candidate; break; }
                    }
                    if (player == null) throw new InvalidOperationException("Story scene has no player to relocate.");
                    float y = arguments.TryGetValue("story-y", out string yText)
                        ? ParseCoordinate(yText, "story-y") : player.GlobalPosition.Y;
                    player.GlobalPosition = new Vector2(relocationX, y);
                    player.Velocity = Vector2.Zero;
                    player.SetPhysicsProcess(false);
                    GD.Print($"[opening-capture] Diagnostic relocation to {player.GlobalPosition}; player physics frozen. This does not prove traversal.");
                    for (int frame = 0; frame < relocationSettle; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                FighterSimulationDriver frozenDriver = null;
                float[] platformUnits = null;
                float[] platformWorldPosition = null;
                if (platformRelocate) {
                    FighterStageGeometry geometry = FighterStageGeometry.ForStage(fighterID);
                    if (platformIndex < 0 || platformIndex >= geometry.Platforms.Length)
                        throw new ArgumentException($"--fighter-platform must be 0..{geometry.Platforms.Length - 1} for {fighterID}.");
                    frozenDriver = (shown as FighterStageController)?.FighterDriver
                        ?? throw new InvalidOperationException("Fighter scene has no initialized simulation driver.");
                    PlayerController player = shown.GetNodeOrNull<PlayerController>("Player1")
                        ?? throw new InvalidOperationException("Fighter scene has no Player1 presentation.");
                    FighterStagePlatform platform = geometry.Platforms[platformIndex];
                    Vector2 destination = new(950f + 62.5f * platform.CenterX.ToFloat(),
                        700f - 62.5f * platform.SurfaceY.ToFloat());
                    frozenDriver.SetProcess(false);
                    frozenDriver.SetPhysicsProcess(false);
                    player.GlobalPosition = destination;
                    platformUnits = new[] { platform.CenterX.ToFloat(), platform.SurfaceY.ToFloat(), platform.HalfWidth.ToFloat() };
                    platformWorldPosition = Vec(destination);
                    GD.Print($"[opening-capture] Diagnostic fighter relocation: P1 -> platform {platformIndex} at {destination}; only simulation driver stopped. This does not prove traversal.");
                    for (int frame = 0; frame < relocationSettle; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Image image = viewport.GetTexture().GetImage();
                string output = ProjectSettings.GlobalizePath(arguments.GetValueOrDefault("out", "user://opening_captures"));
                if (DirAccess.MakeDirRecursiveAbsolute(output) != Error.Ok && !DirAccess.DirExistsAbsolute(output))
                    throw new InvalidOperationException($"Cannot create output directory {output}");
                string relocationSuffix = relocate ? "_x" + relocationX.ToString("0.###", CultureInfo.InvariantCulture) : "";
                if (platformRelocate) relocationSuffix = $"_platform{platformIndex}";
                string prefix = System.IO.Path.Combine(output, $"{target}_{hero}{relocationSuffix}");
                Error saved = image.SavePng(prefix + ".png");
                if (saved != Error.Ok) throw new InvalidOperationException($"SavePng failed: {saved}");
                Camera2D camera = viewport.GetCamera2D();
                var players = new List<object>();
                var openings = new List<object>();
                foreach (Node node in Descendants(shown)) {
                    if (node is PlayerController player) players.Add(DescribePlayer(player));
                    if (node is StoryOpeningPresentation opening) openings.Add(new {
                        level = opening.Profile.Level, requested_fraction = opening.Profile.HeroHeightFraction,
                        achieved_fraction = opening.AchievedHeroHeightFraction,
                        visual_multiplier = opening.HeroVisualMultiplier,
                        frame = Rect(opening.Profile.Frame)
                    });
                }
                var report = new {
                    target, hero, opponent, scene = path, settle_frames = settle,
                    diagnostic_mode = relocate || platformRelocate ? "relocation; not traversal proof" : "static opening",
                    relocation_x = relocate ? (float?)relocationX : null,
                    relocation_settle_physics_frames = relocate || platformRelocate ? relocationSettle : 0,
                    fighter_platform_index = platformRelocate ? (int?)platformIndex : null,
                    fighter_platform_units_center_x_surface_y_half_width = platformUnits,
                    fighter_platform_world_position = platformWorldPosition,
                    fighter_driver_processing = frozenDriver == null ? (bool?)null : frozenDriver.IsProcessing(),
                    fighter_driver_physics_processing = frozenDriver == null ? (bool?)null : frozenDriver.IsPhysicsProcessing(),
                    active_save_slot = GameManager.Instance.CurrentSession.ActiveSaveSlot,
                    tree_paused = GetTree().Paused,
                    diagnostic_presentation_ignores_pause = arguments.ContainsKey("hide-ui"),
                    resumed_checkpoint = shown is StoryLevelControllerBase level ? level.ResumedCheckpointID
                        : shown is Level01Controller florence ? florence.ResumedCheckpointID : null,
                    width = image.GetWidth(), height = image.GetHeight(),
                    camera = camera == null ? null : new {
                        position = Vec(camera.GlobalPosition), zoom = Vec(camera.Zoom), offset = Vec(camera.Offset),
                        opening_weight = (camera as StoryCameraConfiner)?.OpeningWeight,
                        opening_framing_active = (camera as StoryCameraConfiner)?.HasOpeningFraming,
                        canvas_origin = Vec(viewport.CanvasTransform.Origin),
                        canvas_x = Vec(viewport.CanvasTransform.X), canvas_y = Vec(viewport.CanvasTransform.Y)
                    },
                    players, openings
                };
                string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(prefix + ".json", json);
                GD.Print($"[opening-capture] {prefix}.png\n{json}");
                GetTree().Quit(0);
            } catch (Exception exception) {
                GD.PushError($"[opening-capture] {exception}");
                GetTree().Quit(2);
            }
        }

        private static object DescribePlayer(PlayerController player) {
            AnimatedSprite2D sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            float? figureScale = null;
            if (sprite != null) {
                foreach (Node child in sprite.GetChildren()) {
                    if (child is RetroSpriteScaleNormalizer normalizer) figureScale = normalizer.FigureScale;
                }
            }
            Texture2D texture = sprite?.SpriteFrames?.GetFrameTexture(sprite.Animation, sprite.Frame);
            Image image = texture?.GetImage();
            Rect2 bounds = new();
            if (image != null) {
                Rect2 used = image.GetUsedRect();
                Vector2 offset = sprite.Offset - (sprite.Centered ? texture.GetSize() / 2f : Vector2.Zero);
                used.Position += offset;
                bounds = sprite.GetGlobalTransformWithCanvas() * used;
            }
            return new {
                name = player.Name.ToString(), root_position = Vec(player.GlobalPosition), root_scale = Vec(player.Scale),
                foot_screen = Vec(player.GetGlobalTransformWithCanvas().Origin),
                silhouette_screen = Rect(bounds), visible_height_fraction = bounds.Size.Y / 1080f,
                normalizer_figure_scale = figureScale, physics_processing = player.IsPhysicsProcessing(),
                animation = sprite?.Animation.ToString(), sprite_scale = sprite == null ? null : Vec(sprite.Scale)
            };
        }

        private static IEnumerable<Node> Descendants(Node parent) {
            foreach (Node child in parent.GetChildren()) {
                yield return child;
                foreach (Node nested in Descendants(child)) yield return nested;
            }
        }
        private static float[] Vec(Vector2 vector) => new[] { vector.X, vector.Y };
        private static float ParseCoordinate(string text, string flag) {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
                throw new ArgumentException($"--{flag} must be a finite world coordinate.");
            return value;
        }
        private static float[] Rect(Rect2 rect) => new[] { rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y };
    }
}
