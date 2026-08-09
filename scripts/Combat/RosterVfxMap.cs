using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Maps the roster's authored <c>PresentationEventID</c> strings to the shared
    /// placeholder VFX taxonomy (Package 8 B6).
    ///
    /// <para>The 27 enemies and 15 bosses author 76 <c>EnemyAbilityData</c> hook
    /// strings of the form <c>{enemy|boss}.{sourceID}.{abilityVerb}</c>, plus one
    /// <c>{enemyID}.death</c> per enemy raised by <c>EnemyController</c>. A3's
    /// <see cref="VfxEmitter.EmitForPresentationEvent"/> routes all of them to one
    /// generic pooled effect and left the ID unread; this table gives each one a
    /// silhouette that matches what the ability actually does.</para>
    ///
    /// <para>It matches on the <em>verb tokens</em> of the final segment rather than
    /// on the full ID, so a new roster entry named with the existing vocabulary maps
    /// for free, and one named with a new verb fails
    /// <c>RosterVfxMappingTests</c> loudly instead of silently falling back. That is
    /// the whole point of the design: 42 bespoke scenes would have been unmaintainable
    /// and a silent fallback would have been invisible.</para>
    /// </summary>
    public static class RosterVfxMap {

        /// <summary>Resolved routing for one presentation beat.</summary>
        public readonly struct Mapping {
            public Mapping(VfxEffectFamily family, string poolID, string matchedKeyword, bool hasEffect = true) {
                Family = family;
                PoolID = poolID;
                MatchedKeyword = matchedKeyword;
                HasEffect = hasEffect;
            }

            public VfxEffectFamily Family { get; }

            /// <summary>Pool the effect is drawn from when the taxonomy scene is unavailable.</summary>
            public string PoolID { get; }

            /// <summary>
            /// The verb token that selected this family, or the empty string when the
            /// ID fell through to the generic default.
            /// </summary>
            public string MatchedKeyword { get; }

            /// <summary>
            /// False for beats that deliberately draw nothing. Only the recovery
            /// phase: it fires several times a second on every ability of every
            /// enemy, so an effect there is particle churn, not information — the
            /// same call A2 made when it gave recovery no generic sound.
            /// </summary>
            public bool HasEffect { get; }

            /// <summary>True when a real vocabulary rule matched, not the fallback.</summary>
            public bool IsExplicit => !string.IsNullOrEmpty(MatchedKeyword);
        }

        /// <summary>
        /// Verb vocabulary, in priority order. First list whose keyword appears as a
        /// whole underscore-separated token of the ID's final segment wins.
        ///
        /// <para>Order matters where an ability name carries two verbs:
        /// <c>cross_slash_dash</c> is a slash that happens to move,
        /// <c>shell_burst</c> is a shell that happens to explode, and
        /// <c>lance_lunge</c> is a lance. The earlier list is the noun that carries
        /// the read.</para>
        /// </summary>
        private static readonly (VfxEffectFamily Family, string[] Keywords)[] Vocabulary = {
            // Things that put a new entity on the field.
            (VfxEffectFamily.Summon, new[] {
                "summon", "summons", "minions", "remnants", "deployment", "deploy",
                "call", "levy", "drone", "drones"
            }),
            // Things that travel: shots, arcs, thrown weapons, sustained beams.
            (VfxEffectFamily.Beam, new[] {
                "bolt", "bolts", "arrow", "arrows", "volley", "salvo", "barrage",
                "mortar", "shell", "grapeshot", "spread", "shard", "rocket",
                "javelin", "flintlock", "dagger", "beam", "grid", "rifle",
                "cannon", "autocannon", "laser", "gatling"
            }),
            // Things swung or thrust at contact range.
            (VfxEffectFamily.Slash, new[] {
                "slash", "blade", "blades", "datablade", "cleave", "sweep", "strike",
                "swipe", "gauntlet", "khopesh", "sabre", "cutlass", "lance", "pike",
                "guillotine", "sceptre", "wrench"
            }),
            // Things that expand outward from a point of impact.
            (VfxEffectFamily.Shockwave, new[] {
                "shockwave", "wave", "smash", "slam", "stomp", "tremor", "pulse",
                "nova", "surge", "burst", "collapse", "well", "purge", "vent",
                "veil", "storm", "sandstorm", "quake", "explosion"
            }),
            // Repositions, defensive pops, and everything else that reads as a flash.
            (VfxEffectFamily.Burst, new[] {
                "blink", "rush", "dash", "charge", "step", "lunge", "teleport",
                "exit", "left", "shield", "bubble", "warp", "drop"
            })
        };

        /// <summary>Trailing tokens that name a phase rather than a verb.</summary>
        private static readonly HashSet<string> PhaseTokens = new(StringComparer.Ordinal) {
            "death", "telegraph", "active", "recovery"
        };

        /// <summary>
        /// Resolves the effect for one presentation beat. Always returns a usable
        /// mapping; inspect <see cref="Mapping.IsExplicit"/> to tell a real match
        /// from the fallback.
        /// </summary>
        public static Mapping Resolve(string presentationEventID, EnemyPresentationPhase phase) {
            string pool = phase == EnemyPresentationPhase.Death
                ? VfxEmitter.EnvironmentPoolID
                : VfxEmitter.CombatPoolID;

            // A death beat is the entity coming apart, whatever killed it.
            if (phase == EnemyPresentationPhase.Death) {
                return new Mapping(VfxEffectFamily.Burst, pool, "death");
            }
            if (phase == EnemyPresentationPhase.Recovery) {
                return new Mapping(VfxEffectFamily.Burst, pool, "recovery", hasEffect: false);
            }

            // Family priority dominates token order: an ability naming two verbs is
            // routed by the noun that carries the read, not by which word came first.
            // "pulse_flintlock" is a gun, not a pulse.
            IReadOnlyList<string> tokens = VerbTokens(presentationEventID);
            foreach ((VfxEffectFamily family, string[] keywords) in Vocabulary) {
                foreach (string keyword in keywords) {
                    foreach (string token in tokens) {
                        if (string.Equals(token, keyword, StringComparison.Ordinal)) {
                            return new Mapping(family, pool, keyword);
                        }
                    }
                }
            }
            return new Mapping(VfxEffectFamily.Burst, pool, "");
        }

        /// <summary>True when the authored ID names a boss rather than an enemy.</summary>
        public static bool IsBossEvent(string presentationEventID) =>
            presentationEventID != null && presentationEventID.StartsWith("boss.", StringComparison.Ordinal);

        /// <summary>
        /// The underscore-separated tokens of the ID's final dot segment, in order.
        /// Phase suffixes are stripped so <c>{id}.active</c> resolves the same way as
        /// <c>{id}</c> — A2 hit the same double-suffix trap on the audio side.
        /// </summary>
        public static IReadOnlyList<string> VerbTokens(string presentationEventID) {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(presentationEventID)) return tokens;

            string[] segments = presentationEventID.Split('.', StringSplitOptions.RemoveEmptyEntries);
            int index = segments.Length - 1;
            while (index >= 0 && PhaseTokens.Contains(segments[index])) index--;
            if (index < 0) return tokens;

            foreach (string token in segments[index].Split('_', StringSplitOptions.RemoveEmptyEntries)) {
                tokens.Add(token);
            }
            return tokens;
        }
    }
}
