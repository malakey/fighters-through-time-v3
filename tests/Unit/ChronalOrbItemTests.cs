using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit task: ChronalOrbItem latent wiring. The HP-restore orb must heal
/// against <c>MaximumHP</c> (Resonance bonus / encounter override included, not
/// raw <c>Data.MaxHP</c>) and raise the HP-changed event; the shield-restore
/// orb must go through the authoritative <c>BlockSystem</c>, not the stale
/// <c>PlayerController.CurrentBlockCharges</c> display mirror.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ChronalOrbItemTests {

    [TestCase]
    public void HPRestoreHealsAgainstMaximumHPAndRaisesTheHPChangedEvent() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var orb = new ChronalOrbItem {
            Data = new ChronalOrbData { Effect = OrbEffect.HPRestore, Value = 25f }
        };
        tree.Root.AddChild(orb);
        int eventCount = 0;
        System.Action<PlayerHPPayload> onHPChanged = _ => eventCount++;
        EventBus.Instance.OnPlayerHPChanged += onHPChanged;
        try {
            // A Resonance MaxHP bonus raises the cap above raw Data.MaxHP; the
            // old code clamped the heal to the raw value.
            player.StoryMaxHPBonus = 15;
            player.CurrentHP = player.Data.MaxHP - 5;

            orb.OnPickedUp(player);

            AssertThat(player.CurrentHP).IsEqual(player.MaximumHP);
            AssertThat(player.CurrentHP > player.Data.MaxHP).IsTrue();
            AssertThat(eventCount).IsEqual(1);
        } finally {
            EventBus.Instance.OnPlayerHPChanged -= onHPChanged;
            if (GodotObject.IsInstanceValid(orb)) orb.Free();
            player.Free();
        }
    }

    [TestCase]
    public void ShieldRestoreRefillsTheAuthoritativeBlockSystemAndItsMirror() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var orb = new ChronalOrbItem {
            Data = new ChronalOrbData { Effect = OrbEffect.ShieldRestore, Value = 0f }
        };
        tree.Root.AddChild(orb);
        try {
            var blockSystem = player.GetNode<BlockSystem>("BlockSystem");
            blockSystem.DepleteCharges(2);
            AssertThat(blockSystem.CurrentCharges)
                .IsEqual(player.MaximumBlockCharges - 2);

            orb.OnPickedUp(player);

            AssertThat(blockSystem.CurrentCharges).IsEqual(player.MaximumBlockCharges);
            AssertThat(player.CurrentBlockCharges).IsEqual(blockSystem.CurrentCharges);
        } finally {
            if (GodotObject.IsInstanceValid(orb)) orb.Free();
            player.Free();
        }
    }

    // 2026-10-11 (ledger FIX-STORY-ORB-BUFF-CASES): the switch had no case for
    // the two buff orbs, so picking one up did nothing at all. They now grant
    // the Story drop's temporary buff at the current difficulty's profile.

    [TestCase]
    public void BuffOrbsGrantTheStoryDropBuffAtTheCurrentDifficulty() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var damageOrb = new ChronalOrbItem {
            Data = new ChronalOrbData { Effect = OrbEffect.DamageBoost, Value = 99f, Duration = 99f }
        };
        var speedOrb = new ChronalOrbItem {
            Data = new ChronalOrbData { Effect = OrbEffect.SpeedBuff, Value = 99f, Duration = 99f }
        };
        tree.Root.AddChild(damageOrb);
        tree.Root.AddChild(speedOrb);
        try {
            FTT.Environment.StoryDropTable table = FTT.Environment.StoryDropTable.LoadDefault();
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Easy;
            damageOrb.OnPickedUp(player);
            AssertFloat(player.StoryTemporaryDamageMultiplier)
                .IsEqualApprox(table.GetProfile(Difficulty.Easy).BuffMultiplier, 0.0001f);
            AssertFloat(player.StoryTemporarySpeedMultiplier).IsEqualApprox(1f, 0.0001f);

            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
            speedOrb.OnPickedUp(player);
            AssertFloat(player.StoryTemporarySpeedMultiplier)
                .IsEqualApprox(table.GetProfile(Difficulty.Normal).BuffMultiplier, 0.0001f);
            // The orb's own Value is never read for these two effects.
            AssertFloat(player.StoryTemporaryDamageMultiplier)
                .IsEqualApprox(table.GetProfile(Difficulty.Easy).BuffMultiplier, 0.0001f);
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            if (GodotObject.IsInstanceValid(damageOrb)) damageOrb.Free();
            if (GodotObject.IsInstanceValid(speedOrb)) speedOrb.Free();
            player.Free();
        }
    }

    [TestCase]
    public void OnHardABuffOrbIsConsumedWithoutCancellingARunningBuff() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var damageOrb = new ChronalOrbItem { Data = new ChronalOrbData { Effect = OrbEffect.DamageBoost } };
        var speedOrb = new ChronalOrbItem { Data = new ChronalOrbData { Effect = OrbEffect.SpeedBuff } };
        tree.Root.AddChild(damageOrb);
        tree.Root.AddChild(speedOrb);
        try {
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Hard;
            // Hard authors no buff (design: no temporary buff drops on Hard).
            speedOrb.OnPickedUp(player);
            AssertFloat(player.StoryTemporarySpeedMultiplier).IsEqualApprox(1f, 0.0001f);

            player.ApplyStoryDamageBuff(1.5f, 15f);
            damageOrb.OnPickedUp(player);
            AssertFloat(player.StoryTemporaryDamageMultiplier)
                .OverrideFailureMessage("Hard's empty buff must not overwrite a running one.")
                .IsEqualApprox(1.5f, 0.0001f);
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            if (GodotObject.IsInstanceValid(damageOrb)) damageOrb.Free();
            if (GodotObject.IsInstanceValid(speedOrb)) speedOrb.Free();
            player.Free();
        }
    }
}
