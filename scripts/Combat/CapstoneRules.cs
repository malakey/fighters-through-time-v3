using System;

namespace FTT.Combat {

    /// <summary>
    /// The nine Resonance capstones (gameplay review 2026-11-03, rulings 3a+++ and 3a++++, plan GR-CAP):
    /// one tenth node per hero, Major, tier 4, 375 dust (the price lives in the nine grid resources). The
    /// perk keys and the numbers the effects share live here, once.
    ///
    /// <para>Story only: every reader goes through <c>PlayerController.HasStoryPerk</c>, and nothing under
    /// <c>scripts/FighterSim/</c> may name this class (<c>CapstoneContentTests</c>). Pure: no Godot types.
    /// <b>Every number here is provisional</b> (ruling Q1 accepted the effects, not their numbers; ledger
    /// <c>PROVISIONAL-CAPSTONE-EFFECTS</c>).</para>
    /// </summary>
    public static class CapstoneRules {
        /// <summary>The capstone's tier. <c>ResonanceTierMark</c> draws it with tier 3's art.</summary>
        public const int Tier = 4;

        public const string BannerOfOrleansPerkKey = "banner_of_orleans";
        public const string UnifiedFieldPerkKey = "unified_field";
        public const string CodexAtlanticusPerkKey = "codex_atlanticus";
        public const string EmancipationPerkKey = "emancipation";
        public const string DynastysCursePerkKey = "dynastys_curse";
        public const string WorldSystemPerkKey = "world_system";
        public const string CurtainCallPerkKey = "curtain_call";
        public const string SymphonyNo41PerkKey = "symphony_no_41";
        public const string FreedomLinePerkKey = "freedom_line";

        // --- Joan, Banner of Orleans ---------------------------------------------------
        /// <summary>The wave drops one piece of strip every this many pixels it travels (150 px of wave is 4 pieces).</summary>
        public const float BannerStripSpacingPixels = 50f;
        public const float BannerStripRadiusPixels = 30f;
        public const float BannerStripLifetimeSeconds = 1.5f;
        public const float BannerStripTickSeconds = 0.5f;
        public const float BannerStripTickDamage = 2f;

        // --- Leonardo, Codex Atlanticus --------------------------------------------------
        /// <summary>Charges every turret gains, on top of Clockwork Overdrive's. Leonardo's turret is his one construct.</summary>
        public const int CodexBonusCharges = 1;

        // --- Lincoln, Emancipation -------------------------------------------------------
        public const float EmancipationWaveDamage = 12f;
        public const float EmancipationWaveSpeedPixelsPerSecond = 900f;
        /// <summary>The wave runs from the charge's start to where it stopped, then this far past it.</summary>
        public const float EmancipationWaveOvershootPixels = 120f;
        public const float EmancipationWaveHitstunSeconds = 0.33f;

        // --- Cleopatra, Dynasty's Curse -----------------------------------------------------
        public const double VenomSpreadRadiusUnits = 3.0;
        public const float VenomSpreadSeconds = 3f;
        /// <summary>One death passes its Venom to at most this many mobs, the nearest first.</summary>
        public const int VenomSpreadMaxTargets = 4;

        // --- Shakespeare, Curtain Call ------------------------------------------------------
        public const int CurtainWindboxFrames = 90;
        public const double CurtainWindboxRadiusUnits = 2.0;
        /// <summary>A mob the windbox touches stays open to hazards this long after it leaves.</summary>
        public const int CurtainHazardArmFrames = 30;

        // --- Mozart, Symphony No. 41 --------------------------------------------------------
        public const int SymphonyWindowFrames = 360;
        public const float SymphonyDamagePerStack = 0.1f;
        public const int SymphonyMaxStacks = 3;
    }

    /// <summary>
    /// Mozart's Symphony No. 41 as a small state machine, with no engine in it: casting a different Special
    /// inside the window adds a stack; a repeat of the same Special restarts the window and adds nothing; a
    /// window that runs out clears everything.
    /// </summary>
    public sealed class SymphonyStack {
        private int _lastSlot;
        private int _framesLeft;

        public int Stacks { get; private set; }

        /// <summary>The damage lane: 1.0, plus a tenth a stack.</summary>
        public float DamageScale => 1f + Stacks * CapstoneRules.SymphonyDamagePerStack;

        /// <summary>A Special of <paramref name="slot"/> (1 or 2) was accepted.</summary>
        public void NoteSpecial(int slot) {
            if (_framesLeft > 0 && _lastSlot != slot) {
                Stacks = Math.Min(CapstoneRules.SymphonyMaxStacks, Stacks + 1);
            }
            _lastSlot = slot;
            _framesLeft = CapstoneRules.SymphonyWindowFrames;
        }

        /// <summary>Forgets the stacks, the last Special and the window (the capstone was removed).</summary>
        public void Reset() {
            Stacks = 0;
            _lastSlot = 0;
            _framesLeft = 0;
        }

        /// <summary>One tick of the window.</summary>
        public void Step() {
            if (_framesLeft <= 0 || --_framesLeft > 0) return;
            Stacks = 0;
            _lastSlot = 0;
        }
    }
}
