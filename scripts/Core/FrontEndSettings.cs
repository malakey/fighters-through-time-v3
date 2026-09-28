using System;
using System.Collections.Generic;

namespace FTT.Core {

    // Package 12 W6 — the front-end, settings and accessibility types that the
    // global save payload stores. Everything in this file is plain data with NO
    // engine dependency: GlobalSaveData is constructed and normalized inside
    // pure-C# GdUnit suites that have no Godot runtime, where any engine call is
    // an uncatchable access violation (CLAUDE.md failure signature 7).

    /// <summary>
    /// G10 Text Speed (design Section 12, Gameplay). Stored as an int by the
    /// serializer; the ordinals are a persistence contract — append only.
    /// </summary>
    public enum TextSpeed {
        /// <summary>20 characters per second.</summary>
        Slow = 0,
        /// <summary>30 characters per second — the design default.</summary>
        Normal = 1,
        /// <summary>60 characters per second.</summary>
        Fast = 2,
        /// <summary>The whole line lands at once.</summary>
        Instant = 3
    }

    /// <summary>
    /// G09 "Send crash reports" (design Section 12, About &amp; Privacy). Per the
    /// adopted D9(a) there is <b>no upload endpoint</b>: "Always" keeps a local
    /// report without asking, "Ask" keeps one and offers to open the folder,
    /// "Never" does nothing. Ordinals are a persistence contract.
    /// </summary>
    public enum CrashReportMode {
        Ask = 0,
        Always = 1,
        Never = 2
    }

    /// <summary>
    /// G13 Block Mode (design Section 12, Controls). Toggle is a pure
    /// input-layer latch (adopted D8(a)); it never enters the simulation.
    /// </summary>
    public enum BlockMode {
        Hold = 0,
        Toggle = 1
    }

    /// <summary>
    /// G13 per-device stick processing, applied before input quantization. Local
    /// input processing only: it changes which quantized frame a device produces,
    /// never the rules the simulation applies to a frame, so it is rollback-safe
    /// and stays out of snapshots and hashes.
    /// </summary>
    public sealed class StickProfile {
        /// <summary>Design range for the inner deadzone: 0–40 %.</summary>
        public const float MinDeadzone = 0f;
        public const float MaxDeadzone = 0.4f;
        public const float DefaultDeadzone = 0.15f;

        /// <summary>Clamp domain for the down-input threshold (design default 50 %).</summary>
        public const float MinDownThreshold = 0.2f;
        public const float MaxDownThreshold = 0.9f;
        public const float DefaultDownThreshold = 0.5f;

        /// <summary>Left-stick radial inner deadzone, as a fraction of full deflection.</summary>
        public float Deadzone = DefaultDeadzone;

        /// <summary>Processed downward deflection that reads as Down (crouch, fast-fall, drop-through).</summary>
        public float DownThreshold = DefaultDownThreshold;

        public void Normalize() {
            Deadzone = float.IsFinite(Deadzone)
                ? Math.Clamp(Deadzone, MinDeadzone, MaxDeadzone)
                : DefaultDeadzone;
            DownThreshold = float.IsFinite(DownThreshold)
                ? Math.Clamp(DownThreshold, MinDownThreshold, MaxDownThreshold)
                : DefaultDownThreshold;
        }

        public StickProfile Clone() => new() { Deadzone = Deadzone, DownThreshold = DownThreshold };
    }

    /// <summary>
    /// Pure rules for the G13 stick profiles and the per-device lookup. The
    /// device key is the joypad GUID the engine reports; <see cref="DefaultKey"/>
    /// is the profile every controller without its own entry uses.
    /// </summary>
    public static class StickProfiles {
        public const string DefaultKey = "default";

        /// <summary>The profile for a device key: its own entry, else the default entry, else the design defaults.</summary>
        public static StickProfile Resolve(IReadOnlyDictionary<string, StickProfile> profiles, string deviceKey) {
            if (profiles != null) {
                if (!string.IsNullOrWhiteSpace(deviceKey)
                    && profiles.TryGetValue(deviceKey, out StickProfile own) && own != null) return own;
                if (profiles.TryGetValue(DefaultKey, out StickProfile fallback) && fallback != null) return fallback;
            }
            return DesignDefault;
        }

        /// <summary>Shared immutable-by-convention design default. Never mutate it.</summary>
        public static readonly StickProfile DesignDefault = new();

        /// <summary>
        /// Radial inner deadzone with rescale: inside the deadzone the stick reads
        /// neutral; outside it the remaining travel is stretched back to 0..1 so a
        /// larger deadzone never costs the player full deflection.
        /// </summary>
        public static (float X, float Y) ApplyRadialDeadzone(float x, float y, float deadzone) {
            if (!float.IsFinite(x) || !float.IsFinite(y)) return (0f, 0f);
            float dz = Math.Clamp(deadzone, 0f, 0.95f);
            float magnitude = MathF.Sqrt((x * x) + (y * y));
            if (magnitude <= dz || magnitude <= 0f) return (0f, 0f);
            float clampedMagnitude = MathF.Min(magnitude, 1f);
            float scaled = (clampedMagnitude - dz) / (1f - dz);
            float factor = scaled / magnitude;
            return (Math.Clamp(x * factor, -1f, 1f), Math.Clamp(y * factor, -1f, 1f));
        }

        /// <summary>Whether a processed stick Y (positive = down) reads as a held Down.</summary>
        public static bool IsDownHeld(float processedY, float downThreshold) =>
            float.IsFinite(processedY) && processedY >= Math.Clamp(
                downThreshold, StickProfile.MinDownThreshold, StickProfile.MaxDownThreshold);
    }

    /// <summary>
    /// The G13 Toggle Block latch — a pure input-layer state machine (D8(a)).
    ///
    /// <para>In <see cref="BlockMode.Toggle"/> a Block press latches the stance
    /// and the next Block press releases it. The player's own Jump or Roll press
    /// also releases it on the frame it happens, so those verbs come out as
    /// themselves; a grab chord formed while latched releases it on the following
    /// frame. Charge depletion needs no rule here: the simulation's existing
    /// empty-shield refusal already drops a stance with no charges, and a latched
    /// Block bit is simply "Block held" to it — which is also why shieldstun,
    /// the shatter lockout and the grab/Echo Step exclusions all keep applying
    /// unchanged.</para>
    /// </summary>
    public struct BlockToggleLatch {
        /// <summary>True while the stance is latched on.</summary>
        public bool Latched;

        /// <summary>Last frame's raw (physical) Block state, for press-edge detection.</summary>
        public bool RawBlockPrevious;

        /// <summary>
        /// Advances one frame and returns the Block state the frame should carry.
        /// In Hold mode it is the raw state and the latch is cleared.
        /// </summary>
        public bool Step(BlockMode mode, bool rawBlock, bool jumpPressed, bool rollPressed) {
            bool blockPressed = rawBlock && !RawBlockPrevious;
            RawBlockPrevious = rawBlock;
            if (mode != BlockMode.Toggle) {
                Latched = false;
                return rawBlock;
            }
            if (blockPressed) Latched = !Latched;
            if (jumpPressed || rollPressed) Latched = false;
            return Latched;
        }

        /// <summary>A grab chord fired while latched: the stance is spent.</summary>
        public void ReleaseAfterGrab() => Latched = false;

        public void Reset() {
            Latched = false;
            RawBlockPrevious = false;
        }
    }

    /// <summary>
    /// G10 reveal rates (design Section 12: Slow 20 / Normal 30 / Fast 60 /
    /// Instant). The one table the dialogue reveal reads.
    /// </summary>
    public static class TextSpeedRules {
        public const float NormalCharactersPerSecond = 30f;

        /// <summary>Characters per second, or <see cref="float.PositiveInfinity"/> for Instant.</summary>
        public static float CharactersPerSecond(TextSpeed speed) => speed switch {
            TextSpeed.Slow => 20f,
            TextSpeed.Fast => 60f,
            TextSpeed.Instant => float.PositiveInfinity,
            _ => NormalCharactersPerSecond
        };

        /// <summary>Translation key naming a speed in Settings.</summary>
        public static string LabelKey(TextSpeed speed) => speed switch {
            TextSpeed.Slow => "settings_text_speed_slow",
            TextSpeed.Fast => "settings_text_speed_fast",
            TextSpeed.Instant => "settings_text_speed_instant",
            _ => "settings_text_speed_normal"
        };

        public static TextSpeed Sanitize(TextSpeed speed) =>
            Enum.IsDefined(typeof(TextSpeed), speed) ? speed : TextSpeed.Normal;
    }

    /// <summary>What the next boot does about a previous abnormal exit (G09, D9(a)).</summary>
    public enum CrashReportAction {
        /// <summary>No crash, or the player chose Never.</summary>
        None = 0,
        /// <summary>Keep a local report; show nothing (Always).</summary>
        KeepLocalReport = 1,
        /// <summary>Keep a local report and offer to open the logs folder (Ask).</summary>
        KeepLocalReportAndPrompt = 2
    }

    /// <summary>The pure G09 decision table. There is no upload anywhere in it.</summary>
    public static class CrashReportPolicy {
        public static CrashReportAction Resolve(bool previousSessionCrashed, CrashReportMode mode) {
            if (!previousSessionCrashed) return CrashReportAction.None;
            return mode switch {
                CrashReportMode.Never => CrashReportAction.None,
                CrashReportMode.Always => CrashReportAction.KeepLocalReport,
                _ => CrashReportAction.KeepLocalReportAndPrompt
            };
        }

        public static string LabelKey(CrashReportMode mode) => mode switch {
            CrashReportMode.Always => "settings_crash_reports_always",
            CrashReportMode.Never => "settings_crash_reports_never",
            _ => "settings_crash_reports_ask"
        };
    }

    /// <summary>
    /// G14: lowering the campaign difficulty from the hub. Pure rules; the engine
    /// side (session + save write) lives on <c>CampaignDifficultyService</c>.
    /// </summary>
    public static class CampaignDifficultyRules {

        /// <summary>Why a lowering request was refused, or <see cref="Allowed"/>.</summary>
        public enum Verdict {
            Allowed = 0,
            AlreadyEasiest = 1,
            NotAtHub = 2,
            MidLevelAttempt = 3,
            ActIII = 4,
            CampaignCompleted = 5,
            NoCampaign = 6
        }

        /// <summary>
        /// Design Section 7 "Lowering Difficulty": from the hub, between levels
        /// and never mid-level, one step down (Hard → Normal → Easy); never up;
        /// no change inside the Act III gauntlet (which has no hub visit).
        /// </summary>
        public static Verdict Evaluate(
            bool hasCampaign,
            Difficulty current,
            bool atHub,
            bool attemptParkedMidLevel,
            bool insideActIIIGauntlet,
            bool campaignCompleted) {
            if (!hasCampaign) return Verdict.NoCampaign;
            if (campaignCompleted) return Verdict.CampaignCompleted;
            if (!atHub) return Verdict.NotAtHub;
            if (insideActIIIGauntlet) return Verdict.ActIII;
            if (attemptParkedMidLevel) return Verdict.MidLevelAttempt;
            if (current <= Difficulty.Easy) return Verdict.AlreadyEasiest;
            return Verdict.Allowed;
        }

        /// <summary>One step easier. Easy stays Easy — there is nothing below it.</summary>
        public static Difficulty OneStepLower(Difficulty current) => current switch {
            Difficulty.Hard => Difficulty.Normal,
            _ => Difficulty.Easy
        };

        /// <summary>
        /// The lowest tier this playthrough has used. <see cref="StorySaveData.LowestDifficultyUsed"/>
        /// initializes to the <b>highest</b> tier so that a payload written before
        /// the field existed reads back as its own <c>Difficulty</c> through this
        /// minimum — the field alone would say "Hard" for everyone.
        /// </summary>
        public static Difficulty EffectiveLowest(StorySaveData save) {
            if (save == null) return Difficulty.Normal;
            return save.LowestDifficultyUsed < save.Difficulty ? save.LowestDifficultyUsed : save.Difficulty;
        }

        /// <summary>
        /// <b>Declared v7 derivation</b> (Package 12 plan §3 item 2, "lowestDifficultyUsed
        /// is seeded from Difficulty"). Phase C calls this once per story payload
        /// in the single v6 → v7 step. Idempotent; never raises the value.
        /// </summary>
        public static void SeedLowestDifficultyUsed(StorySaveData save) {
            if (save == null) return;
            save.LowestDifficultyUsed = EffectiveLowest(save);
        }

        /// <summary>
        /// Applies one step down to the payload and records the new lowest tier.
        /// Returns false (and changes nothing) when already Easy. Committed
        /// Integrity scores, rewards and completed levels are untouched — the new
        /// tier only reaches the next level entry.
        /// </summary>
        public static bool LowerOneStep(StorySaveData save) {
            if (save == null || save.Difficulty <= Difficulty.Easy) return false;
            Difficulty next = OneStepLower(save.Difficulty);
            SeedLowestDifficultyUsed(save);
            save.Difficulty = next;
            if (next < save.LowestDifficultyUsed) save.LowestDifficultyUsed = next;
            return true;
        }
    }
}
