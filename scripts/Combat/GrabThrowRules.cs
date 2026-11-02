namespace FTT.Combat {

    /// <summary>The three throws. Values match <c>FighterGrabRules.ThrowForward/Up/Back</c>.</summary>
    public enum GrabThrowDirection {
        Forward = 0,
        Up = 1,
        Back = 2
    }

    /// <summary>
    /// What the grabber's stick says about the throw, already classified by the
    /// caller: neutral, toward the victim (forward), away (back) or up.
    /// </summary>
    public enum GrabThrowIntent {
        Neutral = 0,
        Forward = 1,
        Up = 2,
        Back = 3
    }

    /// <summary>
    /// Grab hold-phase rules shared by both modes (2026-10-31 grab pass, step A).
    /// Pure and engine-free: the sim feeds it integer stick values, Story
    /// classifies its own float stick into a <see cref="GrabThrowIntent"/>.
    ///
    /// <para>A connected grab throws on one of two triggers: <b>early</b> — once
    /// <see cref="BasicComboRules.ThrowEarlyMinFrames"/> hold ticks have elapsed,
    /// a fresh Attack press or any held direction throws at once — or
    /// <b>auto</b> — when <see cref="BasicComboRules.ThrowDecisionFrames"/> hold
    /// ticks have elapsed the throw goes whatever is held (neutral = forward).
    /// A direction held from before the catch counts. The grab's own chord press
    /// cannot count as the early press: it was made 14 ticks before the catch,
    /// and only the press edge of the current hold tick is passed in.</para>
    /// </summary>
    public static class GrabThrowRules {
        /// <summary>The sim's stick threshold (of ±127) for a held direction.</summary>
        public const int StickThreshold = 30;

        /// <summary>
        /// Classifies an integer stick (±127, +Y down as in the sim, so up is
        /// negative). Up beats back beats forward; neutral is neither.
        /// </summary>
        public static GrabThrowIntent ClassifyStick(int moveX, int moveY, bool facingRight) {
            if (moveY < -StickThreshold) return GrabThrowIntent.Up;
            if (moveX > StickThreshold) return facingRight ? GrabThrowIntent.Forward : GrabThrowIntent.Back;
            if (moveX < -StickThreshold) return facingRight ? GrabThrowIntent.Back : GrabThrowIntent.Forward;
            return GrabThrowIntent.Neutral;
        }

        /// <summary>The throw an intent picks; neutral is the forward throw.</summary>
        public static GrabThrowDirection DirectionFor(GrabThrowIntent intent) => intent switch {
            GrabThrowIntent.Up => GrabThrowDirection.Up,
            GrabThrowIntent.Back => GrabThrowDirection.Back,
            _ => GrabThrowDirection.Forward
        };

        /// <summary>
        /// Whether the hold throws this tick. <paramref name="holdFramesElapsed"/>
        /// counts hold ticks already spent before this one (the catch tick is 0).
        /// </summary>
        public static bool ThrowTriggerThisTick(
            int holdFramesElapsed, bool attackPressedEdge, GrabThrowIntent intent,
            out GrabThrowDirection direction) {
            direction = DirectionFor(intent);
            if (holdFramesElapsed >= BasicComboRules.ThrowDecisionFrames) return true;
            return holdFramesElapsed >= BasicComboRules.ThrowEarlyMinFrames
                && (attackPressedEdge || intent != GrabThrowIntent.Neutral);
        }
    }
}
