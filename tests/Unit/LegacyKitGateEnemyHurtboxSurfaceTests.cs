using System;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 — the <c>EnemyHurtbox</c> option on a <see cref="LegacyKitGate"/>'s
/// strike surface, which retires Package 11's <c>LegacyResonantEffigy</c> stand-in.
///
/// <para>The kit deliveries this exists for (Divine Piercing's thrusts, the Clockwork
/// Turret's bolts, the Tesla Coil's arcs, the Serpent Nest's bites) are hand-rolled
/// <c>PhysicsShapeQueryParameters2D</c> sweeps masked to <c>EnemyHurtbox</c> alone,
/// which skip only a hurtbox owned by the attacker and then call
/// <see cref="Hurtbox.TakeHit"/> directly, crediting meter only on damage dealt &gt; 0.
/// These cases reproduce exactly that contract against the gate surface: it is on the
/// queried layer, it latches on the authored ability, and it can never pay out as an
/// enemy would.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LegacyKitGateEnemyHurtboxSurfaceTests {

    private const string RequiredID = "w8_probe_sweep_ability";

    [TestCase]
    public void TheStrikeSurfaceSitsOnEnemyHurtboxByDefaultAndTheOptionIsConfigurable() {
        using var fixture = new SurfaceFixture();
        EnvironmentHurtboxAdapter surface = fixture.Gate.StrikeSurface;
        AssertObject(surface).IsNotNull();
        AssertThat(fixture.Gate.StrikeSurfaceOnEnemyHurtbox).IsTrue();
        AssertThat((long)surface.CollisionLayer)
            .IsEqual((long)(CollisionLayers.PersistentObject | CollisionLayers.EnemyHurtbox));

        fixture.Gate.ConfigureStrikeSurface(onEnemyHurtbox: false);
        AssertThat((long)surface.CollisionLayer).IsEqual((long)CollisionLayers.PersistentObject);

        fixture.Gate.ConfigureStrikeSurface(LegacyKitGate.SweepStrikeSurfaceSize, onEnemyHurtbox: true);
        AssertThat((long)surface.CollisionLayer)
            .IsEqual((long)(CollisionLayers.PersistentObject | CollisionLayers.EnemyHurtbox));
        var shape = surface.GetChild<CollisionShape2D>(0);
        AssertThat(((RectangleShape2D)shape.Shape).Size).IsEqual(LegacyKitGate.SweepStrikeSurfaceSize);
    }

    [TestCase]
    public void AnEnemyHurtboxOnlyDeliveryStrikesTheGateAndEarnsNoEnemyCredit() {
        using var fixture = new SurfaceFixture();
        EnvironmentHurtboxAdapter surface = fixture.Gate.StrikeSurface;

        // The sweep's own filter: masked to EnemyHurtbox, skipping only the attacker's own hurtbox.
        const int attackerIndex = 0;
        AssertThat((surface.CollisionLayer & CollisionLayers.EnemyHurtbox) != 0)
            .OverrideFailureMessage("An EnemyHurtbox-masked sweep cannot see the gate surface.").IsTrue();
        AssertThat(surface.OwnerPlayerIndex).IsNotEqual(attackerIndex);

        float dealt = surface.TakeHit(new HitPayload {
            AttackerIndex = attackerIndex,
            AttackID = RequiredID,
            HitboxID = "w8_sweep",
            AttackClass = AttackClass.Special,
            Damage = 12f
        });

        AssertThat(fixture.Gate.IsResolved)
            .OverrideFailureMessage("The authored ability's sweep did not open its gate.").IsTrue();
        // Zero damage dealt is what keeps every meter / Rally / reward path silent:
        // the kits credit only when dealt > 0.
        AssertThat(dealt).IsEqual(0f);

        // It is a mechanism, not an enemy: unowned, outside every enemy group, and no
        // enemy or boss controller above it for the kits' owner walks to find.
        AssertThat(surface.OwnerPlayerIndex).IsEqual(-1);
        AssertThat(surface.IsInGroup("Enemies")).IsFalse();
        AssertThat(fixture.Gate.IsInGroup("Enemies")).IsFalse();
        for (Node node = surface; node != null; node = node.GetParent()) {
            AssertThat(node is EnemyController || node is BossController)
                .OverrideFailureMessage($"The gate surface sits under an enemy controller ({node.Name}).")
                .IsFalse();
        }
    }

    [TestCase]
    public void AnotherAbilityOrAnEnemyAttackerNeverLatchesTheSurface() {
        using var fixture = new SurfaceFixture();
        EnvironmentHurtboxAdapter surface = fixture.Gate.StrikeSurface;

        AssertThat(surface.TakeHit(new HitPayload { AttackerIndex = 0, AttackID = "combo_1", Damage = 5f }))
            .IsEqual(0f);
        AssertThat(fixture.Gate.IsResolved)
            .OverrideFailureMessage("Another attack opened a kit gate through the EnemyHurtbox surface.").IsFalse();

        // An enemy hit carries the unowned index; even with the right ID it is ignored.
        AssertThat(surface.TakeHit(new HitPayload { AttackerIndex = -1, AttackID = RequiredID, Damage = 5f }))
            .IsEqual(0f);
        AssertThat(fixture.Gate.IsResolved)
            .OverrideFailureMessage("An enemy attack opened a kit gate.").IsFalse();
    }

    [TestCase]
    public void ALatchedGateDropsTheEnemyHurtboxLayerSoItStopsDrawingConstructFire() {
        using var fixture = new SurfaceFixture();
        EnvironmentHurtboxAdapter surface = fixture.Gate.StrikeSurface;

        fixture.Gate.ForceResolve();
        AssertThat((surface.CollisionLayer & CollisionLayers.EnemyHurtbox) != 0)
            .OverrideFailureMessage("A solved gate still answers EnemyHurtbox sweeps (turret/coil target lock).")
            .IsFalse();
        AssertThat((surface.CollisionLayer & CollisionLayers.PersistentObject) != 0).IsTrue();

        // Re-configuring a solved gate never re-arms the layer.
        fixture.Gate.ConfigureStrikeSurface(onEnemyHurtbox: true);
        AssertThat((surface.CollisionLayer & CollisionLayers.EnemyHurtbox) != 0).IsFalse();
    }

    private sealed class SurfaceFixture : IDisposable {
        public readonly LegacyKitGate Gate;

        public SurfaceFixture() {
            Gate = new LegacyKitGate {
                Name = "W8SurfaceGate",
                GateID = "w8_surface_gate",
                RequiredAbilityID = RequiredID,
                Mode = LegacyGateMode.Strike,
                Position = new Vector2(-12000f, -4000f)
            };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Gate);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Gate)) Gate.Free();
        }
    }
}
