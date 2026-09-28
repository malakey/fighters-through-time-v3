using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6. The engine-free rules behind the front-end work: G13's Toggle
/// Block latch and stick processing, G09's crash-report decision table, G14's
/// lowering rules and the declared v7 derivation, and the additive save fields'
/// legacy defaults.
///
/// <para><b>Pure C# — no <c>[RequireGodotRuntime]</c>.</b> That is itself part of
/// the contract: every type here, and the save payloads, must stay usable with no
/// Godot runtime (CLAUDE.md failure signature 7).</para>
/// </summary>
[TestSuite]
public class FrontEndSettingsRulesTests {

    [TestCase]
    public void TheToggleLatchFlipsOnBlockPressesAndPassesHoldThrough() {
        var latch = new BlockToggleLatch();
        // Hold mode: the raw state, and never a latch.
        AssertThat(latch.Step(BlockMode.Hold, rawBlock: true, false, false)).IsTrue();
        AssertThat(latch.Step(BlockMode.Hold, rawBlock: false, false, false)).IsFalse();
        AssertThat(latch.Latched).IsFalse();

        latch.Reset();
        // Toggle: a press latches, releasing the button keeps it latched...
        AssertThat(latch.Step(BlockMode.Toggle, true, false, false)).IsTrue();
        AssertThat(latch.Step(BlockMode.Toggle, false, false, false)).IsTrue();
        AssertThat(latch.Step(BlockMode.Toggle, false, false, false)).IsTrue();
        // ...and the next press releases it.
        AssertThat(latch.Step(BlockMode.Toggle, true, false, false)).IsFalse();
        AssertThat(latch.Step(BlockMode.Toggle, false, false, false)).IsFalse();
    }

    [TestCase]
    public void JumpRollAndAGrabChordAreLegalExitsFromALatchedStance() {
        var latch = new BlockToggleLatch();
        latch.Step(BlockMode.Toggle, true, false, false);
        latch.Step(BlockMode.Toggle, false, false, false);
        // Jump releases on the frame it is pressed, so the jump comes out as a jump.
        AssertThat(latch.Step(BlockMode.Toggle, false, jumpPressed: true, rollPressed: false)).IsFalse();

        latch.Step(BlockMode.Toggle, true, false, false);
        latch.Step(BlockMode.Toggle, false, false, false);
        AssertThat(latch.Step(BlockMode.Toggle, false, jumpPressed: false, rollPressed: true)).IsFalse();

        latch.Step(BlockMode.Toggle, true, false, false);
        latch.Step(BlockMode.Toggle, false, false, false);
        latch.ReleaseAfterGrab();
        AssertThat(latch.Step(BlockMode.Toggle, false, false, false)).IsFalse();

        // Switching back to Hold never leaves a stale latch.
        latch.Step(BlockMode.Toggle, true, false, false);
        AssertThat(latch.Step(BlockMode.Hold, false, false, false)).IsFalse();
        AssertThat(latch.Latched).IsFalse();
    }

    [TestCase]
    public void TheRadialDeadzoneZeroesInsideAndRescalesOutside() {
        AssertThat(StickProfiles.ApplyRadialDeadzone(0.1f, 0.05f, 0.15f)).IsEqual((0f, 0f));
        (float x, float y) = StickProfiles.ApplyRadialDeadzone(1f, 0f, 0.15f);
        AssertThat(x).IsEqualApprox(1f, 0.0001f);
        AssertThat(y).IsEqualApprox(0f, 0.0001f);
        // Halfway through the live travel reads as half deflection.
        (float mid, _) = StickProfiles.ApplyRadialDeadzone(0.575f, 0f, 0.15f);
        AssertThat(mid).IsEqualApprox(0.5f, 0.001f);
        // Direction is preserved on a diagonal.
        (float dx, float dy) = StickProfiles.ApplyRadialDeadzone(0.6f, 0.6f, 0.2f);
        AssertThat(dx).IsEqualApprox(dy, 0.0001f);
        // Garbage in, neutral out.
        AssertThat(StickProfiles.ApplyRadialDeadzone(float.NaN, 0.5f, 0.15f)).IsEqual((0f, 0f));

        AssertThat(StickProfiles.IsDownHeld(0.49f, 0.5f)).IsFalse();
        AssertThat(StickProfiles.IsDownHeld(0.5f, 0.5f)).IsTrue();
        AssertThat(StickProfiles.IsDownHeld(0.35f, 0.3f)).IsTrue();
    }

    [TestCase]
    public void StickProfilesResolvePerDeviceThenDefaultThenDesign() {
        var profiles = new System.Collections.Generic.Dictionary<string, StickProfile> {
            [StickProfiles.DefaultKey] = new() { Deadzone = 0.25f },
            ["pad-guid"] = new() { Deadzone = 0.05f, DownThreshold = 0.8f }
        };
        AssertThat(StickProfiles.Resolve(profiles, "pad-guid").Deadzone).IsEqual(0.05f);
        AssertThat(StickProfiles.Resolve(profiles, "other").Deadzone).IsEqual(0.25f);
        AssertThat(StickProfiles.Resolve(null, "other").Deadzone).IsEqual(StickProfile.DefaultDeadzone);
        AssertThat(StickProfile.DefaultDeadzone).IsEqual(0.15f);
        AssertThat(StickProfile.DefaultDownThreshold).IsEqual(0.5f);

        var wild = new StickProfile { Deadzone = 0.9f, DownThreshold = float.NaN };
        wild.Normalize();
        AssertThat(wild.Deadzone).IsEqual(StickProfile.MaxDeadzone);
        AssertThat(wild.DownThreshold).IsEqual(StickProfile.DefaultDownThreshold);
    }

    [TestCase]
    public void CrashReportsNeverUploadAndFollowAskAlwaysNever() {
        AssertThat(CrashReportPolicy.Resolve(false, CrashReportMode.Ask)).IsEqual(CrashReportAction.None);
        AssertThat(CrashReportPolicy.Resolve(true, CrashReportMode.Ask)).IsEqual(CrashReportAction.KeepLocalReportAndPrompt);
        AssertThat(CrashReportPolicy.Resolve(true, CrashReportMode.Always)).IsEqual(CrashReportAction.KeepLocalReport);
        AssertThat(CrashReportPolicy.Resolve(true, CrashReportMode.Never)).IsEqual(CrashReportAction.None);
        AssertThat(new GlobalSaveData().CrashReports).IsEqual(CrashReportMode.Ask);
    }

    [TestCase]
    public void LoweringIsHubOnlyBetweenLevelsNeverUpAndNeverInActIII() {
        const bool yes = true, no = false;
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Hard, yes, no, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.Allowed);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Normal, yes, no, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.Allowed);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Easy, yes, no, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.AlreadyEasiest);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Hard, no, no, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.NotAtHub);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Hard, yes, yes, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.MidLevelAttempt);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Hard, yes, yes, yes, no))
            .IsEqual(CampaignDifficultyRules.Verdict.ActIII);
        AssertThat(CampaignDifficultyRules.Evaluate(yes, Difficulty.Hard, yes, no, no, yes))
            .IsEqual(CampaignDifficultyRules.Verdict.CampaignCompleted);
        AssertThat(CampaignDifficultyRules.Evaluate(no, Difficulty.Hard, yes, no, no, no))
            .IsEqual(CampaignDifficultyRules.Verdict.NoCampaign);

        AssertThat(CampaignDifficultyRules.OneStepLower(Difficulty.Hard)).IsEqual(Difficulty.Normal);
        AssertThat(CampaignDifficultyRules.OneStepLower(Difficulty.Normal)).IsEqual(Difficulty.Easy);
        AssertThat(CampaignDifficultyRules.OneStepLower(Difficulty.Easy)).IsEqual(Difficulty.Easy);
    }

    [TestCase]
    public void LoweringOneStepRecordsTheLowestTierAndNeverRaises() {
        var save = new StorySaveData { Difficulty = Difficulty.Hard, LowestDifficultyUsed = Difficulty.Hard };
        AssertThat(CampaignDifficultyRules.LowerOneStep(save)).IsTrue();
        AssertThat(save.Difficulty).IsEqual(Difficulty.Normal);
        AssertThat(save.LowestDifficultyUsed).IsEqual(Difficulty.Normal);
        AssertThat(CampaignDifficultyRules.LowerOneStep(save)).IsTrue();
        AssertThat(save.Difficulty).IsEqual(Difficulty.Easy);
        AssertThat(save.LowestDifficultyUsed).IsEqual(Difficulty.Easy);
        AssertThat(CampaignDifficultyRules.LowerOneStep(save)).IsFalse();
        AssertThat(save.Difficulty).IsEqual(Difficulty.Easy);
    }

    /// <summary>
    /// The declared v7 derivation, "lowestDifficultyUsed is seeded from
    /// Difficulty": a payload written before the field existed loads with the
    /// initializer (Hard) and must read back as its own tier; seeding persists that.
    /// </summary>
    [TestCase]
    public void TheV7DerivationSeedsLowestDifficultyUsedFromDifficulty() {
        StorySaveData legacy = JsonConvert.DeserializeObject<StorySaveData>(
            "{\"SelectedCharacterID\":\"joan\",\"Difficulty\":0}");
        AssertThat(legacy.LowestDifficultyUsed).IsEqual(Difficulty.Hard);
        AssertThat(CampaignDifficultyRules.EffectiveLowest(legacy)).IsEqual(Difficulty.Easy);
        CampaignDifficultyRules.SeedLowestDifficultyUsed(legacy);
        AssertThat(legacy.LowestDifficultyUsed).IsEqual(Difficulty.Easy);
        // Idempotent, and never raises an already-lower record.
        CampaignDifficultyRules.SeedLowestDifficultyUsed(legacy);
        AssertThat(legacy.LowestDifficultyUsed).IsEqual(Difficulty.Easy);

        var lowered = new StorySaveData { Difficulty = Difficulty.Normal, LowestDifficultyUsed = Difficulty.Easy };
        CampaignDifficultyRules.SeedLowestDifficultyUsed(lowered);
        AssertThat(lowered.LowestDifficultyUsed).IsEqual(Difficulty.Easy);
    }

    [TestCase]
    public void TheAdditiveGlobalFieldsLoadTheirLegacyDefaultsAndRoundTrip() {
        // A v6 payload that predates every W6 field.
        GlobalSaveData legacy = JsonConvert.DeserializeObject<GlobalSaveData>("{\"MasterVolume\":0.5}");
        legacy.Normalize();
        AssertThat(legacy.FirstRunSetupCompleted)
            .OverrideFailureMessage("An existing install must never be shown the first-run cards.").IsTrue();
        AssertThat(legacy.PhotosensitivityNoticeSeen).IsFalse();
        AssertThat(legacy.DialogueTextSpeed).IsEqual(TextSpeed.Normal);
        AssertThat(legacy.MuteWhenUnfocused).IsTrue();
        AssertThat(legacy.BlockInputMode).IsEqual(BlockMode.Hold);
        AssertThat(legacy.MasterMuted || legacy.MusicMuted || legacy.SFXMuted || legacy.UIMuted).IsFalse();
        AssertThat(legacy.StickProfiles.Count).IsEqual(0);

        legacy.DialogueTextSpeed = TextSpeed.Instant;
        legacy.CrashReports = CrashReportMode.Never;
        legacy.BlockInputMode = BlockMode.Toggle;
        legacy.SFXMuted = true;
        legacy.StickProfiles[StickProfiles.DefaultKey] = new StickProfile { Deadzone = 0.3f, DownThreshold = 0.7f };
        GlobalSaveData back = JsonConvert.DeserializeObject<GlobalSaveData>(JsonConvert.SerializeObject(legacy));
        back.Normalize();
        AssertThat(back.DialogueTextSpeed).IsEqual(TextSpeed.Instant);
        AssertThat(back.CrashReports).IsEqual(CrashReportMode.Never);
        AssertThat(back.BlockInputMode).IsEqual(BlockMode.Toggle);
        AssertThat(back.SFXMuted).IsTrue();
        AssertThat(back.StickProfiles[StickProfiles.DefaultKey].Deadzone).IsEqualApprox(0.3f, 0.0001f);

        // Hand-edited garbage is repaired, not trusted.
        back.DialogueTextSpeed = (TextSpeed)99;
        back.CrashReports = (CrashReportMode)99;
        back.BlockInputMode = (BlockMode)99;
        back.Normalize();
        AssertThat(back.DialogueTextSpeed).IsEqual(TextSpeed.Normal);
        AssertThat(back.CrashReports).IsEqual(CrashReportMode.Ask);
        AssertThat(back.BlockInputMode).IsEqual(BlockMode.Hold);
    }

    [TestCase]
    public void TextSpeedRatesMatchTheDesignTable() {
        AssertThat(TextSpeedRules.CharactersPerSecond(TextSpeed.Slow)).IsEqual(20f);
        AssertThat(TextSpeedRules.CharactersPerSecond(TextSpeed.Normal)).IsEqual(30f);
        AssertThat(TextSpeedRules.CharactersPerSecond(TextSpeed.Fast)).IsEqual(60f);
        AssertThat(float.IsPositiveInfinity(TextSpeedRules.CharactersPerSecond(TextSpeed.Instant))).IsTrue();
        AssertThat(new GlobalSaveData().DialogueTextSpeed).IsEqual(TextSpeed.Normal);
    }
}
