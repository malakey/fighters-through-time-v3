namespace FTT.UI {

    /// <summary>
    /// Package 8 B1. The visibility state machine behind a standard/elite enemy's
    /// overhead HP bar and name label.
    ///
    /// Those bars used to be authored visible and stayed visible for the whole
    /// level, so a room of six patrolling mobs rendered six always-on bars over
    /// placeholder geometry — clutter that carries no information until the player
    /// has actually engaged. The rule here is: nothing until the enemy takes its
    /// first hit, full opacity while the fight is live, then a fade once the
    /// exchange has been quiet for a while. Bosses are not affected; they use the
    /// HUD's big bar and have no overhead bar at all.
    ///
    /// Pure C# on purpose. It is owned by a pooled node, so the reset contract
    /// (a recycled body must not inherit the previous occupant's bar state) is the
    /// part most worth pinning, and pinning it should not require a scene tree.
    /// </summary>
    public sealed class OverheadBarVisibility {

        /// <summary>Seconds of no incoming damage before the bar begins to fade.</summary>
        public const float DefaultQuietSeconds = 4f;

        /// <summary>Seconds the fade itself takes once the quiet period elapses.</summary>
        public const float DefaultFadeSeconds = 1f;

        public float QuietSeconds { get; set; } = DefaultQuietSeconds;
        public float FadeSeconds { get; set; } = DefaultFadeSeconds;

        private float _remaining;

        /// <summary>
        /// True once this enemy has been damaged at least since the last
        /// <see cref="Reset"/>. A pooled body that has never been hit in its
        /// current life reports false even if its previous occupant was killed.
        /// </summary>
        public bool HasBeenDamaged { get; private set; }

        /// <summary>Current bar alpha, 0 (hidden) to 1 (fully shown).</summary>
        public float Alpha { get; private set; }

        /// <summary>Convenience for the node side: anything above zero draws.</summary>
        public bool IsVisible => Alpha > 0f;

        /// <summary>Seconds left before the bar is fully hidden.</summary>
        public float RemainingSeconds => _remaining;

        /// <summary>
        /// Returns the state to "never engaged". Called from both
        /// <c>OnSpawn</c> and <c>OnDespawn</c> so neither direction of a pool
        /// cycle can leak a visible bar onto the next spawn.
        /// </summary>
        public void Reset() {
            HasBeenDamaged = false;
            Alpha = 0f;
            _remaining = 0f;
        }

        /// <summary>
        /// Shows the bar at full opacity and restarts the quiet window. Every hit
        /// re-arms it, so a sustained fight never flickers.
        /// </summary>
        public void NotifyDamaged() {
            HasBeenDamaged = true;
            Alpha = 1f;
            _remaining = TotalWindowSeconds;
        }

        /// <summary>
        /// Advances the timer. Safe to call every frame regardless of state; an
        /// undamaged or already-hidden enemy is a no-op.
        /// </summary>
        public void Tick(float deltaSeconds) {
            if (_remaining <= 0f) return;
            if (deltaSeconds > 0f) _remaining -= deltaSeconds;

            if (_remaining <= 0f) {
                _remaining = 0f;
                Alpha = 0f;
                return;
            }

            float fade = FadeSeconds > 0f ? FadeSeconds : 0f;
            Alpha = _remaining < fade ? _remaining / fade : 1f;
        }

        private float TotalWindowSeconds {
            get {
                float quiet = QuietSeconds > 0f ? QuietSeconds : 0f;
                float fade = FadeSeconds > 0f ? FadeSeconds : 0f;
                // A configuration with neither window still shows one frame of bar
                // rather than nothing, so a hit is never completely silent.
                float total = quiet + fade;
                return total > 0f ? total : float.Epsilon;
            }
        }
    }
}
