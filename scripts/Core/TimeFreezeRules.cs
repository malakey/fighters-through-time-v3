namespace FTT.Core {

    /// <summary>
    /// Gameplay review 2026-11-03 (decision 10b++, plan §1.3): the one switch that turns the
    /// player's Time Freeze verb off while keeping every line of it.
    ///
    /// <para><b>Dormant when <see cref="Enabled"/> is false:</b> the
    /// <c>TimeFreezeController</c> node (the bootstrapper never builds it), its input
    /// (<c>gameplay_time_freeze</c> has no events and is in neither
    /// <c>InputManager.RemappableActions</c> nor <c>InputShortcuts.DirectActions</c>), the Story HUD
    /// indicator, the Level 0 lesson and the first-use tooltip.</para>
    ///
    /// <para><b>Still live:</b> the controller's statics (<c>FreezeSeconds</c>, <c>CooldownSeconds</c>,
    /// <c>FrozenTimeGroups</c>, <c>ProcessFallbackGroups</c>, <c>ThawWarningLabelKey</c>,
    /// <c>ControllerGroup</c>), <c>IStoryTimeFreezable</c>, the thaw cue, the
    /// <c>AudioSnapshot.TimeFreeze</c> ladder and the freeze sweeps, because the Post-Landing Hold and
    /// boss T01b suspension (<c>ChronalRewindManager</c>) use them. Nothing here may delete or gate those.</para>
    ///
    /// <para>The rule is a code constant with a test seam (the <c>MainMenu.ShowDeveloperLevelSelect</c>
    /// pattern), deliberately not a Settings toggle, which would be save state. Re-enabling is one reviewed
    /// edit of <see cref="Default"/> plus restoring the project action's events and the two action lists
    /// (ledger <c>DEFER-TIME-FREEZE-OFF</c>). Engine-free, so a pure-C# suite can read it. Story only: nothing
    /// under <c>scripts/FighterSim/</c> may name this type.</para>
    /// </summary>
    public static class TimeFreezeRules {

        /// <summary>The shipped value. False since 2026-11-03.</summary>
        public const bool Default = false;

        /// <summary>
        /// Test seam: when set, replaces <see cref="Default"/>. A suite that exercises the verb sets it to
        /// true in a <c>try</c> and restores the previous value in <c>finally</c>.
        /// </summary>
        internal static bool? EnabledOverrideForTesting;

        /// <summary>Whether the player's Time Freeze verb exists in this build.</summary>
        public static bool Enabled => EnabledOverrideForTesting ?? Default;
    }
}
