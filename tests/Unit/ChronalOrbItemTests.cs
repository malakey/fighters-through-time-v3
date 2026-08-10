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
}
