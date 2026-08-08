using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Decides which vertical music layer a Story scene should be sitting on
    /// (Package 8 B5). Pure C#: no Godot types, no scene-tree access, so the whole
    /// ambient → combat → climax policy is testable frame by frame without a
    /// running level.
    ///
    /// <para><b>Engagement, not presence.</b> Combat is driven by enemies the player
    /// is actually near, not by every enemy alive in the level. Campaign levels spawn
    /// their opening room's enemies at load, so "any live enemy" would pin every
    /// level to the combat layer from the first frame and the ambient bed would never
    /// be heard. <see cref="StoryAudioDirector"/> supplies the engaged count.</para>
    ///
    /// <para><b>Falling back is held, rising is immediate.</b> Dropping to ambient the
    /// instant the last enemy dies makes the music flap during a staggered wave, so a
    /// disengage waits <see cref="CombatHoldSeconds"/>. Engaging is immediate — the
    /// crossfade itself supplies the ramp.</para>
    ///
    /// <para>Boss engagement outranks both: once the encounter reveals, the layer is
    /// <see cref="StemIntensity.Climax"/> until the boss dies or the level ends.</para>
    /// </summary>
    public sealed class StoryCombatIntensityModel {
        public const float DefaultCombatHoldSeconds = 4.0f;

        /// <summary>Seconds combat persists after the last engaged enemy is gone.</summary>
        public float CombatHoldSeconds = DefaultCombatHoldSeconds;

        /// <summary>True between the boss reveal and its defeat.</summary>
        public bool BossEngaged { get; private set; }

        /// <summary>Layer the scene should currently be on.</summary>
        public StemIntensity Current { get; private set; } = StemIntensity.Ambient;

        /// <summary>Seconds of combat hold still owed. Zero once the hold has run out.</summary>
        public float HoldRemaining { get; private set; }

        public void SetBossEngaged(bool engaged) {
            BossEngaged = engaged;
            // Leaving a boss fight must not immediately land on ambient: the room is
            // usually still full of adds, and the hold gives the mix somewhere to
            // settle while the level's post-boss beat plays.
            if (!engaged) HoldRemaining = CombatHoldSeconds;
        }

        /// <summary>
        /// Advances the model one presentation step and returns the resolved layer.
        /// <paramref name="engagedEnemyCount"/> is the number of living hostiles
        /// within the director's engagement radius.
        /// </summary>
        public StemIntensity Advance(int engagedEnemyCount, float deltaSeconds) {
            if (engagedEnemyCount > 0) {
                HoldRemaining = CombatHoldSeconds;
            } else if (HoldRemaining > 0f) {
                HoldRemaining = deltaSeconds >= HoldRemaining ? 0f : HoldRemaining - deltaSeconds;
            }

            Current = BossEngaged
                ? StemIntensity.Climax
                : engagedEnemyCount > 0 || HoldRemaining > 0f
                    ? StemIntensity.Combat
                    : StemIntensity.Ambient;
            return Current;
        }

        /// <summary>Returns the model to a fresh scene's state.</summary>
        public void Reset() {
            BossEngaged = false;
            HoldRemaining = 0f;
            Current = StemIntensity.Ambient;
        }
    }
}
