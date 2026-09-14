using System.Collections.Generic;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / F24. Node-path conformance for <c>scenes/ui/StoryHUD.tscn</c>
/// against <c>docs/design-contracts/HUD_CONTRACT.md</c>.
///
/// <para>This suite exists because of what the scene used to be. Four widgets the
/// design names as authored nodes — the Rally echo band, the Integrity readout,
/// the boss intro card and the retired rewind-cooldown pip — were built in code at
/// runtime with comments saying "built in code so the authored scene stays
/// untouched". That is invisible to every scene test and to anyone opening the
/// scene in the editor, and it is exactly how the HUD drifted a whole hierarchy
/// away from its contract. Pinning the node paths is what makes the next drift
/// loud.</para>
///
/// <para>The contract does not enumerate the repo's own level title, objective,
/// boss bar, boss intro card or checkpoint toast. Those are shipped behaviour, so
/// they are kept as named siblings under <c>SafeArea</c> and pinned here as
/// accepted deviations rather than quietly deleted to make a document match.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudContractTests {

    /// <summary>Every node HUD_CONTRACT names, at its contract path.</summary>
    private static readonly string[] ContractNodes = {
        "SafeArea",
        "SafeArea/TopLeft_Panel",
        "SafeArea/TopLeft_Panel/PlayerPortrait",
        "SafeArea/TopLeft_Panel/Vitals/HealthBar_BG",
        "SafeArea/TopLeft_Panel/Vitals/HealthBar_BG/RallyEcho_Band",
        "SafeArea/TopLeft_Panel/Vitals/BlockCharges_Panel",
        "SafeArea/TopLeft_Panel/Vitals/StatusEffects_Panel",
        "SafeArea/TopLeft_Panel/Vitals/StatusEffects_Panel/DamageStatus_Indicator",
        "SafeArea/TopLeft_Panel/Vitals/StatusEffects_Panel/DamageStatus_Indicator/DamageStatus_DurationRadial",
        "SafeArea/TopLeft_Panel/Vitals/StatusEffects_Panel/ControlStatus_Indicator",
        "SafeArea/TopLeft_Panel/Vitals/StatusEffects_Panel/ControlStatus_Indicator/ControlStatus_DurationRadial",
        "SafeArea/TopLeft_Panel/Vitals/MeterRow/UltimateMeter_BG",
        "SafeArea/TopLeft_Panel/Vitals/MeterRow/UltimateMeter_BG/UltimateMeter_Fill",
        "SafeArea/TopLeft_Panel/Vitals/MeterRow/UltimateMeter_BG/UltimateMeter_LockOverlay",
        "SafeArea/TopLeft_Panel/Vitals/MeterRow/DefySeal",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown1_Icon",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown1_Icon/Cooldown1_Radial",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown1_Icon/Cooldown1_LockOverlay",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown2_Icon",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown2_Icon/Cooldown2_Radial",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown2_Icon/Cooldown2_LockOverlay",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown3_Icon",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown3_Icon/Cooldown3_Radial",
        "SafeArea/TopLeft_Panel/Vitals/CooldownRow/Cooldown3_Icon/Cooldown3_LockOverlay",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/RewindCounter",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/RewindCounter/RewindIcon",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/RewindCounter/RewindCountText",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/TimeFreezeIndicator",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/BeaconAnchors",
        "SafeArea/TopLeft_Panel/Vitals/TemporalRow/BeaconAnchors/BeaconIcon",
        "SafeArea/TopRight_Panel",
        "SafeArea/TopRight_Panel/CurrencyContainer",
        "SafeArea/TopRight_Panel/CurrencyContainer/ChronalDustIcon",
        "SafeArea/TopRight_Panel/CurrencyContainer/ChronalDustText",
        "SafeArea/TopRight_Panel/CurrencyContainer/DustPickup_Float",
        "SafeArea/TopRight_Panel/IntegrityClock",
        "SafeArea/TopRight_Panel/IntegrityClock/ClockFace",
        "SafeArea/TopRight_Panel/IntegrityClock/ClockWedge",
        "SafeArea/TopRight_Panel/IntegrityClock/SiphonStreams",
        "SafeArea/TopRight_Panel/IntegrityClock/IntegrityText"
    };

    /// <summary>Shipped extras the contract does not enumerate; kept deliberately.</summary>
    private static readonly string[] AcceptedExtras = {
        "SafeArea/TopLeft/LevelTitle",
        "SafeArea/TopLeft/Objective",
        "SafeArea/BossPanel/BossName",
        "SafeArea/BossPanel/BossBar",
        "SafeArea/BossPanel/BossBar/PhaseNotches",
        "SafeArea/BossIntroCard",
        "SafeArea/CheckpointToast"
    };

    [TestCase]
    public void EveryNodeTheContractNamesExistsAtItsContractPath() {
        Node host = CreateHost("StoryHudContractHost");
        try {
            StoryHUD hud = AddHud(host);
            var missing = new List<string>();
            foreach (string path in ContractNodes) {
                if (hud.GetNodeOrNull(path) == null) missing.Add(path);
            }
            if (missing.Count > 0) {
                AssertThat("HUD_CONTRACT nodes missing from StoryHUD.tscn: " + string.Join(", ", missing))
                    .IsEqual("");
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheShippedExtrasSurviveTheRewriteAsNamedSiblings() {
        Node host = CreateHost("StoryHudExtrasHost");
        try {
            StoryHUD hud = AddHud(host);
            var missing = new List<string>();
            foreach (string path in AcceptedExtras) {
                if (hud.GetNodeOrNull(path) == null) missing.Add(path);
            }
            if (missing.Count > 0) {
                AssertThat("Accepted repo extras lost in the rewrite: " + string.Join(", ", missing))
                    .IsEqual("");
            }
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// F03 retires the 12 s manual rewind, so the V7.3 cooldown pip that measured
    /// it must be gone rather than repurposed — shipping the Time Freeze indicator
    /// beside a live rewind-cooldown pip would put two temporal clocks on screen
    /// counting different things.
    /// </summary>
    [TestCase]
    public void TheRetiredRewindCooldownPipIsNotReintroduced() {
        Node host = CreateHost("StoryHudNoPipHost");
        try {
            StoryHUD hud = AddHud(host);
            hud._Process(1.0 / 60.0);
            AssertThat(FindNamed(hud, "RewindCooldownPip"))
                .OverrideFailureMessage(
                    "RewindCooldownPip is the retired 12 s manual-rewind readout (F03).")
                .IsFalse();

            // The Time Freeze indicator is what occupies that slot now, and its
            // readiness is independent of the death-rewind count beside it.
            AssertObject(hud.GetNodeOrNull(
                "SafeArea/TopLeft_Panel/Vitals/TemporalRow/TimeFreezeIndicator")).IsNotNull();
        } finally {
            host.Free();
        }
    }

    private static bool FindNamed(Node root, string name) {
        if (root.Name.ToString() == name) return true;
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (FindNamed(child, name)) return true;
        }
        return false;
    }

    private static StoryHUD AddHud(Node host) {
        StoryHUD hud = StoryHUD.CreateDefault();
        host.AddChild(hud);
        return hud;
    }

    private static Node CreateHost(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }
}
