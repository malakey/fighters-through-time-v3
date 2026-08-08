using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B7: contracts for the shared <see cref="StageLightingRig"/> and for
/// every scene that instances it.
///
/// <para>Two failure modes drive this suite, and neither shows up in a headless
/// smoke run because both render perfectly happily — just wrongly.</para>
///
/// <para><b>A texture-less <see cref="PointLight2D"/> emits nothing.</b> The
/// repository shipped exactly one Light2D before this workstream
/// (<c>StoryItemPickupTemplate</c>'s <c>Glow</c>) and it had no texture, so it had
/// been inert since it was authored. A rig whose lights lose their gradient is the
/// same bug at ten times the scale, and the scene still loads clean.</para>
///
/// <para><b>A <see cref="CanvasModulate"/> multiplies the whole canvas layer,
/// fighters included.</b> Characters here are flat placeholder silhouettes with no
/// rim lighting to survive a crush, so an atmospheric tone that reads as "moody" in
/// a screenshot can cost real combat legibility. Every authored ambient tone is
/// therefore held above <see cref="StageLightingRig.MinimumAmbientChannel"/> on
/// every channel; era identity comes from the channel ratio and the light colours,
/// not from darkness.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StageLightingRigTests {

    private const string RigPath = "res://scenes/templates/StageLightingRig.tscn";
    private const string HubPath = "res://scenes/campaign/HubWorld.tscn";
    private const string LevelTwoPath = "res://scenes/campaign/Level_02_Orleans.tscn";
    private const string LevelSixPath = "res://scenes/campaign/Level_06_Pompeii.tscn";
    private const string PickupTemplatePath = "res://scenes/templates/StoryItemPickupTemplate.tscn";

    [TestCase]
    public void TheTemplateCarriesAnAmbientModulateAndThreeTexturedLights() {
        AssertThat(ResourceLoader.Exists(RigPath)).IsTrue();
        var rig = ResourceLoader.Load<PackedScene>(RigPath).Instantiate<StageLightingRig>();
        try {
            AssertObject(rig.Ambient).IsNotNull();
            foreach (PointLight2D light in new[] { rig.KeyLight, rig.FillLight, rig.RimLight }) {
                AssertObject(light).IsNotNull();
                AssertObject(light.Texture).OverrideFailureMessage(
                    $"'{light?.Name}' has no texture; a texture-less PointLight2D emits nothing.")
                    .IsNotNull();
            }

            // The authored node defaults and the script defaults must agree, or the
            // template renders one tone in the editor and another the moment _Ready
            // pushes the exports over it.
            AssertColorsMatch(rig.Ambient.Color, rig.AmbientTone, "template ambient default");
        } finally {
            rig.Free();
        }
    }

    /// <summary>
    /// The exports are the whole tuning surface: a stage overrides them in one node
    /// block rather than reaching into the instanced scene's children with fragile
    /// <c>index=</c> blocks. Nothing enforces that they actually reach the children,
    /// so this walks the real path — construct, enter the tree, read the nodes.
    /// </summary>
    [TestCase]
    public void EveryExportReachesItsChildNodeWithoutEnteringTheTree() {
        var rig = ResourceLoader.Load<PackedScene>(RigPath).Instantiate<StageLightingRig>();
        try {
            rig.AmbientTone = new Color(0.9f, 0.8f, 0.7f);
            rig.KeyColor = new Color(1f, 0.2f, 0.1f);
            rig.KeyEnergy = 1.25f;
            rig.KeyOffset = new Vector2(123f, 456f);
            rig.KeyScale = 7.5f;
            rig.RimEnabled = false;

            AssertColorsMatch(rig.Ambient.Color, new Color(0.9f, 0.8f, 0.7f), "ambient");
            AssertColorsMatch(rig.KeyLight.Color, new Color(1f, 0.2f, 0.1f), "key colour");
            AssertThat(rig.KeyLight.Energy).IsEqualApprox(1.25f, 0.0001f);
            AssertThat(rig.KeyLight.Position).IsEqual(new Vector2(123f, 456f));
            AssertThat(rig.KeyLight.TextureScale).IsEqualApprox(7.5f, 0.0001f);
            AssertThat(rig.KeyLight.Enabled).IsTrue();
            AssertThat(rig.RimLight.Enabled).OverrideFailureMessage(
                "RimEnabled = false must actually disable the rim light.").IsFalse();

            // A zero-energy light is disabled outright rather than left drawing a
            // black quad's worth of state for nothing.
            rig.FillEnergy = 0f;
            AssertThat(rig.FillLight.Enabled).IsFalse();
        } finally {
            rig.Free();
        }
    }

    /// <summary>
    /// All ten Fighter stages plus the hub and the two exemplar campaign levels
    /// instance the rig, and every one of them keeps its ambient tone readable.
    /// </summary>
    [TestCase]
    public void EveryLitSceneInstancesTheRigWithAReadableAmbientTone() {
        var issues = new List<string>();
        int scenesChecked = 0;

        foreach (string scenePath in LitScenePaths()) {
            AssertThat(ResourceLoader.Exists(scenePath)).OverrideFailureMessage(
                $"'{scenePath}' does not exist.").IsTrue();
            Node root = ResourceLoader.Load<PackedScene>(scenePath).Instantiate();
            try {
                StageLightingRig rig = FindRig(root);
                if (rig == null) {
                    issues.Add($"{scenePath}: no StageLightingRig instance");
                    continue;
                }
                scenesChecked++;

                Color tone = rig.AmbientTone;
                float floor = StageLightingRig.MinimumAmbientChannel;
                if (tone.R < floor || tone.G < floor || tone.B < floor) {
                    issues.Add($"{scenePath}: ambient tone {tone} crushes a channel below {floor}");
                }
                if (tone.A < 1f) {
                    issues.Add($"{scenePath}: ambient tone alpha {tone.A} — a CanvasModulate " +
                               "alpha below 1 fades the entire canvas layer");
                }
                if (rig.KeyEnergy <= 0f) {
                    issues.Add($"{scenePath}: key light has no energy");
                }
            } finally {
                root.Free();
            }
        }

        AssertThat(scenesChecked).OverrideFailureMessage(
            $"Only {scenesChecked} lit scenes were verified.").IsEqual(13);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Each era gets its own tone and key colour. Ten stages that all instance the
    /// template and never override it would satisfy every other assertion here while
    /// delivering one lighting look for the whole catalog.
    /// </summary>
    [TestCase]
    public void EveryFighterStageTunesTheRigToItsOwnEra() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        var tones = new List<Color>();
        var keyColors = new List<Color>();

        foreach (FighterStageData stage in catalog.Stages) {
            Node root = ResourceLoader.Load<PackedScene>(stage.ScenePath).Instantiate();
            try {
                StageLightingRig rig = FindRig(root);
                AssertObject(rig).OverrideFailureMessage(
                    $"{stage.StageID} does not instance the StageLightingRig.").IsNotNull();
                tones.Add(rig.AmbientTone);
                keyColors.Add(rig.KeyColor);

                // Fighter stages author their lights against real geometry, so the
                // camera-following mode belongs to the campaign levels only.
                AssertThat(rig.FollowsCamera).OverrideFailureMessage(
                    $"{stage.StageID} sets FollowsCamera; Fighter stages are one authored screen.")
                    .IsFalse();
            } finally {
                root.Free();
            }
        }

        AssertThat(Distinct(tones)).OverrideFailureMessage(
            "The ten stages share ambient tones; the rig was instanced without era tuning.")
            .IsGreaterEqual(9);
        AssertThat(Distinct(keyColors)).OverrideFailureMessage(
            "The ten stages share key-light colours.").IsGreaterEqual(8);
    }

    /// <summary>
    /// The repository's one pre-existing <see cref="PointLight2D"/> had no texture
    /// and had therefore never emitted anything. Fixed here rather than left as a
    /// silent counter-example beside a rig that gets it right.
    /// </summary>
    [TestCase]
    public void TheStoryPickupGlowIsNoLongerAnInertTexturelessLight() {
        Node root = ResourceLoader.Load<PackedScene>(PickupTemplatePath).Instantiate();
        try {
            var glow = root.GetNodeOrNull<PointLight2D>("Glow");
            AssertObject(glow).IsNotNull();
            AssertObject(glow.Texture).OverrideFailureMessage(
                "StoryItemPickupTemplate/Glow is a texture-less PointLight2D and emits nothing.")
                .IsNotNull();
            AssertThat(glow.Energy > 0f).IsTrue();
        } finally {
            root.Free();
        }
    }

    private static IEnumerable<string> LitScenePaths() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        foreach (FighterStageData stage in catalog.Stages) yield return stage.ScenePath;
        yield return HubPath;
        yield return LevelTwoPath;
        yield return LevelSixPath;
    }

    private static StageLightingRig FindRig(Node node) {
        if (node is StageLightingRig rig) return rig;
        foreach (Node child in node.GetChildren()) {
            StageLightingRig found = FindRig(child);
            if (found != null) return found;
        }
        return null;
    }

    private static void AssertColorsMatch(Color actual, Color expected, string label) {
        AssertThat(actual.IsEqualApprox(expected)).OverrideFailureMessage(
            $"{label}: expected {expected}, got {actual}").IsTrue();
    }

    private static int Distinct(List<Color> colors) {
        var seen = new List<Color>();
        foreach (Color color in colors) {
            bool duplicate = false;
            foreach (Color other in seen) {
                if (other.IsEqualApprox(color)) { duplicate = true; break; }
            }
            if (!duplicate) seen.Add(color);
        }
        return seen.Count;
    }
}
