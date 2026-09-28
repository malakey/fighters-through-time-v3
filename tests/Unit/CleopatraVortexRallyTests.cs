using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — closes <c>VERIFY-VORTEX-RECLAIM</c>. D03g: "every Sandstorm
/// Vortex tick, including the final launching tick, is excluded from Rally
/// reclaim." The Story tick used to credit through the default
/// <c>collectsEcho: true</c>; it now stamps its payload as Tick delivery and the
/// credit reads that payload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraVortexRallyTests {

    [TestCase]
    public void NoVortexTickReclaimsRallyTheFinalLaunchingTickIncluded() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController cleopatra = CharacterFactory.CreateCharacter("cleopatra");
        PlayerController victim = CharacterFactory.CreateCharacter("joan", playerIndex: 1);
        tree.Root.AddChild(cleopatra);
        tree.Root.AddChild(victim);
        try {
            // Give Cleopatra a Rally pool and missing HP to reclaim into.
            cleopatra.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                AttackID = "test.hit",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 40f,
                Knockback = Vector2.Zero,
                HitOrigin = new Vector2(20f, 0f)
            });
            float pool = cleopatra.EchoPool;
            AssertFloat(pool).IsGreater(0f);
            int hp = cleopatra.CurrentHP;

            var vortex = cleopatra.GetNode<CleopatraSandstormVortex>("Special2");
            AssertThat(vortex.Data.Delivery).IsEqual(HitDelivery.Tick);
            Hurtbox target = victim.GetNode<Hurtbox>("Hurtbox");

            float churn = vortex.ApplyVortexTickTo(target, finalTick: false);
            float final = vortex.ApplyVortexTickTo(target, finalTick: true);
            AssertFloat(churn + final)
                .OverrideFailureMessage("The ticks must actually deal damage, or the pin proves nothing.")
                .IsGreater(0f);

            AssertThat(cleopatra.CurrentHP)
                .OverrideFailureMessage("A Vortex tick reclaims no Rally (D03g).")
                .IsEqual(hp);
            AssertFloat(cleopatra.EchoPool).IsEqualApprox(pool, 0.001f);

            // Control: an ordinary direct hit from her does reclaim.
            BaseSpecial.CreditDealt(cleopatra, new HitPayload { Delivery = HitDelivery.DirectHit }, 6f);
            AssertThat(cleopatra.CurrentHP > hp).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(cleopatra.PlayerIndex);
            InputManager.Instance?.ClearInputSource(victim.PlayerIndex);
            cleopatra.Free();
            victim.Free();
        }
    }
}
