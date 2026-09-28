using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — GAP-14. F04 permits a free puzzle Ultimate, never free combat
/// damage or reward. Before W4 the Nexus cast invoked the real Ultimate and
/// relied on the cleared arena for isolation. Now every hit the Nexus-authorized
/// cast produces is stamped <see cref="HitPayload.PuzzleOnly"/> and rejected at
/// <c>Hurtbox.TakeHit</c>, so a hostile placed beside the cast takes nothing and
/// the caster earns nothing, while the puzzle target still resolves.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NexusResonanceFenceTests {

    private const string EinsteinUltimateID = "einstein_cosmological_constant";

    [TestCase]
    public void AHostileBesideANexusCastTakesNothingWhileThePuzzleTargetResolves() {
        using var fixture = new FenceFixture();
        EnemyController enemy = fixture.SpawnEnemyBesideTheCast();
        fixture.Meter.SetValue(0f);
        fixture.Player.CurrentUltimateMeter = 0f;
        fixture.Source.Interact(fixture.Player);
        AssertThat(fixture.Source.IsArmed).IsTrue();

        AssertThat(fixture.PressUltimate()).IsTrue();
        AssertThat(fixture.Player.IsNexusCastInFlight).IsTrue();
        AssertThat(fixture.Gate.IsResolved)
            .OverrideFailureMessage("The puzzle target still resolves through the Nexus source.").IsTrue();

        // Every kit-built Ultimate payload is stamped puzzle-only and bounces off.
        BaseSpecial ultimate = fixture.Player.GetNode<BaseSpecial>("Ultimate");
        HitPayload blast = BaseSpecial.WithAbilityContract(new HitPayload {
            AttackerIndex = fixture.Player.PlayerIndex,
            AttackID = EinsteinUltimateID,
            HitboxID = "singularity",
            AttackClass = AttackClass.Ultimate,
            Damage = 40f,
            Knockback = new Vector2(6f, -6f),
            AppliedStatus = StatusType.TimeDilation,
            StatusDuration = 2f
        }, ultimate.Data, fixture.Player);
        AssertThat(blast.PuzzleOnly).IsTrue();

        int enemyHP = enemy.CurrentHP;
        float dealt = enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(blast);
        BaseSpecial.CreditDealt(fixture.Player, in blast, dealt);
        AssertThat(dealt).IsEqual(0f);
        AssertThat(enemy.CurrentHP).IsEqual(enemyHP);
        AssertThat(enemy.ActiveStatuses.Has(StatusType.TimeDilation)).IsFalse();
        AssertThat(fixture.Player.CurrentUltimateMeter)
            .OverrideFailureMessage("No meter from a puzzle cast.").IsEqual(0f);
    }

    [TestCase]
    public void AnOrdinaryUltimateAndAnyNonUltimateHitAreNeverFenced() {
        using var fixture = new FenceFixture();
        // No Nexus authorization: the ordinary meter cast.
        fixture.Meter.SetValue(UltimateMeter.MaxValue);
        fixture.Player.CurrentUltimateMeter = UltimateMeter.MaxValue;
        AssertThat(fixture.PressUltimate()).IsTrue();
        AssertThat(fixture.Player.IsNexusCastInFlight).IsFalse();

        BaseSpecial ultimate = fixture.Player.GetNode<BaseSpecial>("Ultimate");
        HitPayload blast = BaseSpecial.WithAbilityContract(
            new HitPayload { Damage = 10f }, ultimate.Data, fixture.Player);
        AssertThat(blast.PuzzleOnly).IsFalse();
        AssertThat(blast.Origin).IsEqual(HitOrigin.Ultimate);

        BaseSpecial special = fixture.Player.GetNode<BaseSpecial>("Special1");
        HitPayload poke = BaseSpecial.WithAbilityContract(
            new HitPayload { Damage = 10f }, special.Data, fixture.Player);
        AssertThat(poke.PuzzleOnly).IsFalse();
    }

    private sealed class FenceFixture : IDisposable {
        public readonly PlayerController Player;
        public readonly NexusResonanceSource Source;
        public readonly LegacyKitGate Gate;
        public readonly UltimateMeter Meter;
        private readonly StaticBody2D _floor;
        private EnemyController _enemy;

        public FenceFixture() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            _floor = new StaticBody2D {
                Name = "NexusFenceFloor",
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            _floor.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
                Position = new Vector2(0f, 20f)
            });
            Root.AddChild(_floor);

            Player = CharacterFactory.CreateCharacter("einstein");
            Root.AddChild(Player);
            Meter = Player.GetNode<UltimateMeter>("UltimateMeter");
            Gate = new LegacyKitGate {
                Name = "NexusFenceGate",
                GateID = "test_nexus_fence_gate",
                RequiredAbilityID = EinsteinUltimateID,
                Mode = LegacyGateMode.Nexus
            };
            Root.AddChild(Gate);
            Source = new NexusResonanceSource {
                Name = "NexusFenceSource",
                SourceID = "test_nexus_fence_source",
                RequiredUltimateAbilityID = EinsteinUltimateID,
                Gate = Gate
            };
            Root.AddChild(Source);
            Player.GlobalPosition = Source.GlobalPosition;
        }

        private static Node Root => ((SceneTree)Engine.GetMainLoop()).Root;

        public EnemyController SpawnEnemyBesideTheCast() {
            EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
            var data = (EnemyData)canonical.Duplicate();
            PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
            _enemy = scene.Instantiate<EnemyController>();
            _enemy.Data = data;
            Root.AddChild(_enemy);
            _enemy.OnSpawn();
            _enemy.GlobalPosition = Player.GlobalPosition + new Vector2(60f, 0f);
            return _enemy;
        }

        public bool PressUltimate() {
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Ultimate));
            InputManager.Instance.SetInputSource(Player.PlayerIndex, source);
            for (int frame = 0; frame < 12; frame++) {
                Player._PhysicsProcess(1.0 / 60.0);
                if (Player.CurrentState == CharacterState.UsingUltimate) return true;
            }
            return false;
        }

        public void Dispose() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            InputManager.Instance?.ClearInputSource(Player.PlayerIndex);
            Player.NexusUltimateSource = null;
            if (_enemy != null && GodotObject.IsInstanceValid(_enemy)) _enemy.Free();
            if (GodotObject.IsInstanceValid(Source)) Source.Free();
            if (GodotObject.IsInstanceValid(Gate)) Gate.Free();
            if (GodotObject.IsInstanceValid(Player)) Player.Free();
            if (GodotObject.IsInstanceValid(_floor)) _floor.Free();
        }
    }
}
