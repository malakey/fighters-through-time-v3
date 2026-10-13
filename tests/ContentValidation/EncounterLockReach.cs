using System;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GU-L4 (gameplay update 2026-10-12, Levels 4–7): the swing-reach measure the
/// level content suites use to keep a Middle or PreBoss strike surface out of a
/// basic swing from inside the locks its baseline lists — the FIX-E authoring
/// rule's belt and braces (the checkpoint guard is the real rule; Specials and
/// shape queries ignore barriers). Not a test suite: a helper, like
/// <see cref="FontHazardClearance"/>. It needs the Godot runtime (it loads the
/// roster's grids), so only <c>[RequireGodotRuntime]</c> suites may call it.
/// </summary>
internal static class EncounterLockReach {
    /// <summary>A checkpoint's strike surface reaches this far either side of its x (the 60 px fracture box).</summary>
    public const float StrikeSurfaceHalfWidthPixels = 30f;

    /// <summary>The Story hero's half-width (the 40 px body).</summary>
    public const float HeroHalfWidthPixels = 20f;

    /// <summary>
    /// The longest Story basic hit past the hero's pivot over the whole roster:
    /// every grounded string hit (the single air attack takes hit 1's x-span),
    /// widest reach profile, every AttackRange minor of the hero's grid bought.
    /// The same measure as <c>Level02ContentTests.LongestBasicSwingPixels</c>.
    /// </summary>
    public static float LongestBasicSwingPixels() {
        float longest = 0f;
        foreach (string id in CharacterRoster.IDs) {
            float minor = 0f;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(id);
            foreach (ResonanceNodeData node in grid?.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null && node.StatModifierKey == ResonanceStatKeys.AttackRange) minor += node.StatModifierValue;
            }
            FTT.Combat.BasicStringProfile profile = FTT.Combat.BasicComboRules.StringProfileFor(id);
            for (int step = 0; step < FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits.Length; step++) {
                FTT.Characters.PlayerController.ResolveGroundStringBoxPixels(step, profile, 1f + minor,
                    out Vector2 size, out Vector2 offset);
                longest = Mathf.Max(longest, offset.X + size.X * 0.5f);
            }
        }
        return longest;
    }

    /// <summary>
    /// The farthest x a basic swing reaches east from inside an area whose east
    /// edge is <paramref name="areaMaxX"/>: a hero pressed against the exit barrier
    /// (its pivot a half-width short of the edge) swinging east.
    /// </summary>
    public static float EastReachFromInside(float areaMaxX, float longestSwing) =>
        areaMaxX - HeroHalfWidthPixels + longestSwing;
}
