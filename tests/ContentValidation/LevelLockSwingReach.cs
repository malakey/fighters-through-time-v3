using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GU-L8 (2026-10-12), a non-suite helper for the Level 8–11 content suites: the
/// GU-E authoring guide's swing-reach clearance (FIX-E, 2026-10-13). A Middle or
/// PreBoss anchor past an encounter lock keeps its strike surface (the anchor's
/// x ± 30) beyond the farthest a Story basic string hit reaches in front of the
/// pivot of a hero pressed against that lock's closed exit barrier — the roster's
/// widest reach profile with every AttackRange minor of its grid bought, at every
/// string step. The checkpoint guard (<c>CheckpointTrigger.IsHeldByEncounterLock</c>)
/// is the real rule; this keeps the geometry from leaning on it. The measure is
/// Level02ContentTests' (its helper is private to that suite).
/// </summary>
internal static class LevelLockSwingReach {

    /// <summary>The 40 px hero body's half-width.</summary>
    public const float HeroHalfWidthPixels = 20f;

    /// <summary>Half the checkpoint's 60 px strike surface.</summary>
    public const float StrikeSurfaceHalfWidthPixels = 30f;

    /// <summary>The farthest any hero's basic string hit reaches in front of its pivot, in pixels.</summary>
    public static float LongestBasicSwingPixels() {
        float longest = 0f;
        foreach (string id in CharacterRoster.IDs) {
            float minor = 0f;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(id);
            foreach (ResonanceNodeData node in grid?.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null && node.StatModifierKey == ResonanceStatKeys.AttackRange) minor += node.StatModifierValue;
            }
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);
            for (int step = 0; step < BasicComboRules.StringBoxHalfWidthUnits.Length; step++) {
                PlayerController.ResolveGroundStringBoxPixels(step, profile, 1f + minor, out Vector2 size, out Vector2 offset);
                longest = Mathf.Max(longest, offset.X + size.X * 0.5f);
            }
        }
        return longest;
    }

    /// <summary>
    /// The x a swing reaches from inside <paramref name="encounterLock"/>, eastward:
    /// the pivot of a hero pressed against the west face of its exit barrier plus
    /// the longest basic swing.
    /// </summary>
    public static float EastwardReachFrom(EncounterLock encounterLock) =>
        encounterLock.ExitBarrier.Footprint.Position.X - HeroHalfWidthPixels + LongestBasicSwingPixels();

    /// <summary>The west edge of a checkpoint's strike surface.</summary>
    public static float StrikeSurfaceWestEdge(float checkpointX) => checkpointX - StrikeSurfaceHalfWidthPixels;
}
