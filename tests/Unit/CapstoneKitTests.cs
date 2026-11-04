using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static FTT.Tests.Unit.ConstructStrikeKit;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-CAP (gameplay review 2026-11-03, rulings 3a+++ and 3a++++, Q1): one case per hero's capstone, each with
/// the hero who does not own it as its control. The numbers are <see cref="CapstoneRules"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CapstoneKitTests {
    private static readonly Vector2 Origin = new(-82000f, 600f);

    [TestCase]
    public void BannerOfOrleansLeavesAStripOfRadiantGroundBehindTheWave() {
        InHost("CapBanner", host => {
            PlayerController joan = Owner(host, "joan", Origin);
            var smite = joan.GetNode<JoanRighteousSmite>("Special1");
            CastToTheEnd(smite);
            AssertThat(smite.BannerStripPieces).OverrideFailureMessage("Without the capstone the wave leaves nothing.").IsEqual(0);

            joan.StoryAbilityPerks.Add(CapstoneRules.BannerOfOrleansPerkKey);
            CastToTheEnd(smite);
            // 2.5 units of travel, one piece at the start and one every 50 px: 150 / 50 + 1.
            AssertThat(smite.BannerStripPieces).IsEqual(4);

            // The last piece dropped burns what stands on it; a zone tick is not a hit.
            PlaceholderZone piece = null;
            foreach (Node node in host.GetChildren()) if (node is PlaceholderZone zone) piece = zone;
            AssertThat(piece).OverrideFailureMessage("The strip is made of pooled zones.").IsNotNull();
            EnemyController mob = Mob(host, piece.GlobalPosition);
            int before = mob.CurrentHP;
            piece.ApplyTickTo(mob);
            AssertThat(before - mob.CurrentHP).IsEqual((int)CapstoneRules.BannerStripTickDamage);
            AssertThat(mob.HasStatusEffect(StatusType.RadiantBurn)).IsTrue();
        });
    }

    [TestCase]
    public void UnifiedFieldCollapsesTheOpenRiftWhenTheWarpArrives() {
        InHost("CapUnified", host => {
            PlayerController einstein = Owner(host, "einstein", Origin);
            var rift = einstein.GetNode<EinsteinRelativityRift>("Special2");
            var warp = einstein.GetNode<EinsteinRelativityWarp>("MovementAbility");

            OpenRift(rift);
            CastToTheEnd(warp);
            AssertThat(rift.RiftActive).OverrideFailureMessage("Without the capstone the Rift stays open.").IsTrue();

            einstein.StoryAbilityPerks.Add(CapstoneRules.UnifiedFieldPerkKey);
            EnemyController mob = Mob(host, rift.ActiveRiftCenter + new Vector2(30f, 0f));
            CastToTheEnd(warp);
            AssertThat(rift.RiftActive).OverrideFailureMessage("The arrival must end the Rift.").IsFalse();
            AssertThat(rift.IsCollapsing).OverrideFailureMessage("It collapses, it does not just close.").IsTrue();
            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(mob.CurrentHP).IsEqual(9999 - LingeringKitRules.RiftCollapseDamage);

            // No Rift open: the arrival is an ordinary arrival.
            einstein.Velocity = Vector2.Zero;
            CastToTheEnd(warp);
            AssertThat(rift.IsCollapsing).IsFalse();
        });
    }

    [TestCase]
    public async Task CodexAtlanticusBoltsPierceTheLineAndEveryTurretHoldsOneMoreCharge() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D host = NewHost("CapCodex");
        try {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-200f, -400f));
            leonardo.StoryAbilityPerks.Add(CapstoneRules.CodexAtlanticusPerkKey);

            // Through the real deploy path: one more charge, on top of nothing.
            var deploy = leonardo.GetNode<LeonardoClockworkTurret>("Special2");
            AssertThat(deploy.TryExecute()).IsTrue();
            for (int frame = 0; frame < 120 && deploy.IsExecuting; frame++) deploy._PhysicsProcess(Step);
            LeonardoTurretNode deployed = null;
            foreach (Node2D node in leonardo.ActivePersistentObjects) if (node is LeonardoTurretNode t) deployed = t;
            AssertThat(deployed).IsNotNull();
            AssertThat(deployed.MaxCharges).IsEqual(LingeringKitRules.TurretCharges + CapstoneRules.CodexBonusCharges);
            leonardo.ActivePersistentObjects.Remove(deployed);
            deployed.ReturnToPool();

            // Without it: the bolt is spent on its first contact, as the sim's is.
            AbilityData data = deploy.Data;
            LeonardoTurretNode plain = Turret(host, leonardo, Origin);
            EnemyController first = Mob(host, Origin + new Vector2(200f, 0f));
            EnemyController second = Mob(host, Origin + new Vector2(300f, 0f));
            // Two targets standing still: the question is the bolt, not their walking.
            first.SetPhysicsProcess(false);
            second.SetPhysicsProcess(false);
            await FireAtAndWait(tree, leonardo, first);
            AssertThat(first.CurrentHP).IsEqual(9999 - LingeringKitRules.TurretSyncedBoltDamage);
            AssertThat(second.CurrentHP).OverrideFailureMessage("A plain synced bolt stops at the first body.").IsEqual(9999);
            leonardo.ActivePersistentObjects.Remove(plain);
            host.RemoveChild(plain);
            plain.Free();

            // With it: both, and the second contact pays no second strike's Momentum.
            first.CurrentHP = 9999;
            LeonardoTurretNode codex = Turret(host, leonardo, Origin);
            codex.Initialize(data, leonardo, clockworkOverdrive: false, codexAtlanticus: true);
            AssertThat(codex.Charges).IsEqual(LingeringKitRules.TurretCharges + CapstoneRules.CodexBonusCharges);
            float pips = leonardo.MomentumPips;
            await FireAtAndWait(tree, leonardo, first);
            AssertThat(first.CurrentHP).IsEqual(9999 - LingeringKitRules.TurretSyncedBoltDamage);
            AssertThat(second.CurrentHP).OverrideFailureMessage("The bolt must fly on through the line.").IsEqual(9999 - LingeringKitRules.TurretSyncedBoltDamage);
            AssertFloat(leonardo.MomentumPips - pips).IsEqualApprox(
                LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike) / (float)BasicComboRules.MomentumUnitsPerPip, 0.0005f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task APlainSyncedBoltStopsOnAContactThatLandedNothingLikeABlockedOneAndTheCapstoneBoltFliesOn() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D host = NewHost("CapPlainBoltRefused");
        try {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-200f, -400f));
            LeonardoTurretNode plain = Turret(host, leonardo, Origin);
            EnemyController refusing = Mob(host, Origin + new Vector2(200f, 0f));
            EnemyController behind = Mob(host, Origin + new Vector2(300f, 0f));
            refusing.SetPhysicsProcess(false);
            behind.SetPhysicsProcess(false);
            // A contact the target refuses (no damage, nothing landed), as a block would: the sim destroys its bolt after
            // any resolved contact, so the plain Story bolt is spent here too (PROVISIONAL-LEONARDO-PLAIN-BOLT-STOPS).
            refusing.DrillInvulnerable = true;
            OwnerLands(leonardo, refusing);
            for (int frame = 0; frame < 14; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(LiveBolts(host)).OverrideFailureMessage("the plain bolt should be spent by the refused contact").IsEqual(0);
            AssertThat(plain.SyncedBoltsFired).OverrideFailureMessage("a bolt must have been fired").IsEqual(1);
            AssertThat(refusing.CurrentHP).IsEqual(9999);
            AssertThat(behind.CurrentHP).OverrideFailureMessage("A plain synced bolt is spent by a refused contact.").IsEqual(9999);
            leonardo.ActivePersistentObjects.Remove(plain);
            host.RemoveChild(plain);
            plain.Free();

            // The capstone keeps piercing: it flies on through the refusing body.
            LeonardoTurretNode codex = Turret(host, leonardo, Origin);
            codex.Initialize(leonardo.GetNode<LeonardoClockworkTurret>("Special2").Data, leonardo, clockworkOverdrive: false, codexAtlanticus: true);
            await FireAtAndWait(tree, leonardo, refusing);
            AssertThat(behind.CurrentHP).OverrideFailureMessage("The capstone bolt must still pierce.").IsEqual(9999 - LingeringKitRules.TurretSyncedBoltDamage);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void EmancipationLeavesAGroundWaveBehindTheCharge() {
        InHost("CapEmancipation", host => {
            PlayerController lincoln = Owner(host, "lincoln", Origin);
            var charge = lincoln.GetNode<LincolnRailCharge>("MovementAbility");
            charge.EmancipationWave.GroundedProbe = _ => true;
            // Past the ram's reach, inside the wave's run: the harness does not move the hero.
            EnemyController mob = Mob(host, Origin + new Vector2(100f, 0f));

            CastToTheEnd(charge);
            AssertThat(charge.EmancipationWave.Active).OverrideFailureMessage("Without the capstone the charge leaves nothing.").IsFalse();
            AssertThat(mob.CurrentHP).IsEqual(9999);

            lincoln.StoryAbilityPerks.Add(CapstoneRules.EmancipationPerkKey);
            AssertThat(charge.TryExecute()).IsTrue();
            for (int frame = 0; frame < 240 && !charge.EmancipationWave.Active; frame++) charge._PhysicsProcess(Step);
            AssertThat(charge.EmancipationWave.Active).OverrideFailureMessage("The wave leaves as the charge ends.").IsTrue();
            AssertThat(charge.EmancipationWave.Front.X).IsLess(mob.GlobalPosition.X);
            for (int frame = 0; frame < 240 && (charge.EmancipationWave.Active || charge.IsExecuting); frame++) charge._PhysicsProcess(Step);
            AssertThat(mob.CurrentHP).IsEqual(9999 - (int)CapstoneRules.EmancipationWaveDamage);
        });
    }

    [TestCase]
    public void DynastysCursePassesAPoisonedMobsVenomToItsNearestNeighbours() {
        InHost("CapCurse", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-1000f, 0f));
            cleopatra.AddToGroup("Players");
            float reach = (float)CapstoneRules.VenomSpreadRadiusUnits * KitMotionRules.StoryPixelsPerUnit;

            // No capstone: a poisoned death passes nothing on.
            EnemyController control = Mob(host, Origin);
            EnemyController controlNeighbour = Mob(host, Origin + new Vector2(50f, 0f));
            control.ApplyStatusEffect(StatusType.Venom, 5f);
            control.TakeDamage(99999);
            AssertThat(control.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(controlNeighbour.HasStatusEffect(StatusType.Venom)).IsFalse();
            controlNeighbour.TakeDamage(99999);

            cleopatra.StoryAbilityPerks.Add(CapstoneRules.DynastysCursePerkKey);
            // A clean death passes nothing either.
            EnemyController clean = Mob(host, Origin);
            EnemyController witness = Mob(host, Origin + new Vector2(50f, 0f));
            clean.TakeDamage(99999);
            AssertThat(witness.HasStatusEffect(StatusType.Venom)).IsFalse();

            // Six neighbours in reach, nearest first; the two farthest miss out, as does one out of reach.
            var neighbours = new EnemyController[6];
            for (int index = 0; index < neighbours.Length; index++) {
                neighbours[index] = Mob(host, Origin + new Vector2(60f + index * 10f, 0f));
            }
            witness.TakeDamage(99999);
            EnemyController outside = Mob(host, Origin + new Vector2(reach + 40f, 0f));
            EnemyController source = Mob(host, Origin);
            source.ApplyStatusEffect(StatusType.Venom, 5f, 2f);
            source.TakeDamage(99999);
            for (int index = 0; index < neighbours.Length; index++) {
                AssertThat(neighbours[index].HasStatusEffect(StatusType.Venom))
                    .OverrideFailureMessage($"neighbour {index}")
                    .IsEqual(index < CapstoneRules.VenomSpreadMaxTargets);
            }
            AssertThat(outside.HasStatusEffect(StatusType.Venom)).IsFalse();
            AssertThat(neighbours[0].ActiveStatuses.Damage.Intensity).IsEqual(2f);
            AssertFloat(neighbours[0].ActiveStatuses.Damage.RemainingSeconds).IsEqual(CapstoneRules.VenomSpreadSeconds);
        });
    }

    [TestCase]
    public void WorldSystemChainsTheDischargeToChargedCoilsAndRefundsAPip() {
        float range = (float)LingeringKitRules.DischargeOwnerRangeUnits * KitMotionRules.StoryPixelsPerUnit;
        foreach (bool owned in new[] { false, true }) {
            InHost(owned ? "CapWorld" : "CapWorldControl", host => {
                PlayerController tesla = Owner(host, "tesla", Origin);
                if (owned) tesla.StoryAbilityPerks.Add(CapstoneRules.WorldSystemPerkKey);
                TeslaCoilNode near = Coil(host, tesla, Origin + new Vector2(300f, 0f));
                // Out of the Pulse's reach of Tesla, in range of the near coil, with no target of its own.
                TeslaCoilNode far = Coil(host, tesla, Origin + new Vector2(range + 240f, 0f));
                EnemyController mob = Mob(host, Origin + new Vector2(400f, 0f));
                int perCoil = LingeringKitRules.DischargeDamagePerCharge * LingeringKitRules.CoilCharges;
                float before = tesla.MomentumPips;
                var pulse = tesla.GetNode<TeslaLorentzPulse>("Special2");
                AssertThat(pulse.TryExecute()).IsTrue();
                for (int frame = 0; frame < 120 && pulse.IsExecuting; frame++) pulse._PhysicsProcess(Step);

                float detonation = LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation)
                    / (float)BasicComboRules.MomentumUnitsPerPip;
                AssertThat(near.IsCoilDestroyed).IsTrue();
                if (owned) {
                    AssertThat(far.IsCoilDestroyed).OverrideFailureMessage("The chain reaches the far coil.").IsTrue();
                    AssertThat(9999 - mob.CurrentHP - PulseDamage(tesla, pulse, mob)).IsEqual(2 * perCoil);
                    AssertFloat(tesla.MomentumPips - before).IsEqualApprox(1f + detonation, 0.0005f);
                } else {
                    AssertThat(far.IsCoilDestroyed).OverrideFailureMessage("Without the capstone the far coil stays out.").IsFalse();
                    AssertThat(far.Charges).IsEqual(LingeringKitRules.CoilCharges);
                    AssertThat(9999 - mob.CurrentHP - PulseDamage(tesla, pulse, mob)).IsEqual(perCoil);
                    AssertFloat(tesla.MomentumPips - before).IsEqualApprox(detonation, 0.0005f);
                }
            });
        }
    }

    [TestCase]
    public async Task CurtainCallLeavesAWindboxThatPushesAndOpensMobsToHazards() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node2D host = NewHost("CapCurtain");
        try {
            PlayerController shakespeare = Owner(host, "shakespeare", Origin);
            var flight = shakespeare.GetNode<ShakespeareProsperosFlight>("MovementAbility");
            CastToTheEnd(flight);
            AssertThat(flight.CurtainWindboxFramesRemaining).OverrideFailureMessage("Without the capstone the gust leaves nothing.").IsEqual(0);

            shakespeare.StoryAbilityPerks.Add(CapstoneRules.CurtainCallPerkKey);
            AssertThat(flight.TryExecute()).IsTrue();
            for (int frame = 0; frame < 120 && flight.CurtainWindboxFramesRemaining == 0; frame++) flight._PhysicsProcess(Step);
            AssertThat(flight.CurtainWindboxFramesRemaining).IsEqual(CapstoneRules.CurtainWindboxFrames);

            // A mob on its feet in the windbox: pushed away, and a hazard now hurts it, once per source.
            EnemyController mob = Mob(host, flight.CurtainWindboxCentre + new Vector2(50f, 0f));
            AssertThat(mob.IsHazardVulnerable).IsFalse();
            float startX = mob.GlobalPosition.X;
            flight._PhysicsProcess(Step);
            AssertThat(mob.GlobalPosition.X).IsGreater(startX);
            AssertThat(mob.IsHazardVulnerable).OverrideFailureMessage("The windbox arms 7d's vulnerability.").IsTrue();
            AssertThat(mob.ApplyHazardImpact(7ul)).IsEqual(HazardMobRules.StandardImpactDamage);
            AssertThat(mob.ApplyHazardImpact(7ul)).IsEqual(0);

            // Out of it, the opening lapses. The windbox itself is a world object with its own clock.
            mob.GlobalPosition = flight.CurtainWindboxCentre + new Vector2(2000f, 0f);
            for (int frame = 0; frame <= CapstoneRules.CurtainHazardArmFrames + 1; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(mob.IsHazardVulnerable).OverrideFailureMessage("The opening must lapse.").IsFalse();
            for (int frame = 0; frame < CapstoneRules.CurtainWindboxFrames && flight.CurtainWindboxFramesRemaining > 0; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(flight.CurtainWindboxFramesRemaining).IsEqual(0);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ALiveRespecOfSymphonyNo41DropsTheStackLaneAndTheHistoryWithIt() {
        InHost("CapSymphonyRespec", host => {
            PlayerController mozart = Owner(host, "mozart", Origin);
            var chord = mozart.GetNode<MozartRequiemChord>("Special1");
            var wave = mozart.GetNode<MozartFortissimoWave>("Special2");
            mozart.StoryAbilityPerks.Add(CapstoneRules.SymphonyNo41PerkKey);
            CastToTheEnd(chord);
            CastToTheEnd(wave);
            AssertThat(mozart.SymphonyStacks).IsEqual(1);
            AssertFloat(mozart.StoryAbilityDamage(10f, "")).IsEqualApprox(11f, 0.001f);

            // The respec removes the perk before the live re-resolve settles: the lane is gone at once.
            mozart.StoryAbilityPerks.Remove(CapstoneRules.SymphonyNo41PerkKey);
            AssertThat(mozart.StoryAbilityDamage(10f, "")).OverrideFailureMessage("a respec kept the retained stack multiplier").IsEqual(10f);
            AssertThat(mozart.StoryAbilityHitboxDamage(10f)).IsEqual(10f);
            mozart.SettleLiveStoryProgression();
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("the stack history survived the respec").IsEqual(0);

            // Buying it back starts from nothing.
            mozart.StoryAbilityPerks.Add(CapstoneRules.SymphonyNo41PerkKey);
            AssertThat(mozart.StoryAbilityDamage(10f, "")).IsEqual(10f);
        });
    }

    [TestCase]
    public void SymphonyNo41StacksOnDifferentSpecialsInsideTheWindowAndLapsesWhole() {
        InHost("CapSymphony", host => {
            PlayerController mozart = Owner(host, "mozart", Origin);
            var chord = mozart.GetNode<MozartRequiemChord>("Special1");
            var wave = mozart.GetNode<MozartFortissimoWave>("Special2");

            CastToTheEnd(chord);
            CastToTheEnd(wave);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("Without the capstone nothing stacks.").IsEqual(0);
            AssertThat(mozart.StoryAbilityDamage(10f, "")).IsEqual(10f);

            mozart.StoryAbilityPerks.Add(CapstoneRules.SymphonyNo41PerkKey);
            CastToTheEnd(chord);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("One Special is not a difference.").IsEqual(0);
            CastToTheEnd(chord);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("The same Special again is not a difference.").IsEqual(0);
            CastToTheEnd(wave);
            AssertThat(mozart.SymphonyStacks).IsEqual(1);
            AssertFloat(mozart.StoryAbilityDamage(10f, "")).IsEqualApprox(11f, 0.001f);
            AssertFloat(mozart.StoryAbilityHitboxDamage(10f)).IsEqualApprox(11f, 0.001f);
            // The contact half is what a basic swing reads: the lane is not on it.
            AssertThat(mozart.StoryHitboxContactDamageScale("")).IsEqual(1f);

            CastToTheEnd(chord);
            CastToTheEnd(wave);
            CastToTheEnd(chord);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("Three stacks is the cap.").IsEqual(CapstoneRules.SymphonyMaxStacks);

            // The window restarts on each cast; 6 s with none clears the lot.
            mozart.StepCapstonesForTest(CapstoneRules.SymphonyWindowFrames - 1);
            AssertThat(mozart.SymphonyStacks).IsEqual(CapstoneRules.SymphonyMaxStacks);
            mozart.StepCapstonesForTest(1);
            AssertThat(mozart.SymphonyStacks).IsEqual(0);
            AssertThat(mozart.StoryAbilityDamage(10f, "")).IsEqual(10f);
            CastToTheEnd(wave);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("A lapse forgets the last Special too.").IsEqual(0);

            // The window runs on the hero's own tick, beside the Momentum regen.
            CastToTheEnd(chord);
            AssertThat(mozart.SymphonyStacks).IsEqual(1);
            for (int frame = 0; frame < CapstoneRules.SymphonyWindowFrames; frame++) mozart._PhysicsProcess(Step);
            AssertThat(mozart.SymphonyStacks).OverrideFailureMessage("The hero's tick must run the window out.").IsEqual(0);
        });
    }

    [TestCase]
    public void FreedomLineRefundsAPipAndGrantsABarrierOnEachCounter() {
        foreach (bool owned in new[] { false, true }) {
            InHost(owned ? "CapFreedom" : "CapFreedomControl", host => {
                PlayerController tubman = Owner(host, "tubman", Origin);
                if (owned) tubman.StoryAbilityPerks.Add(CapstoneRules.FreedomLinePerkKey);
                var foresight = tubman.GetNode<TubmanForesight>("Special2");
                AssertThat(foresight.TryExecute()).IsTrue();
                for (int frame = 0; frame < foresight.Data.StartupFrames; frame++) foresight._PhysicsProcess(Step);
                AssertThat(foresight.IsWindowOpen).IsTrue();
                float pips = tubman.MomentumPips;
                AssertThat(foresight.TryCounter(new HitPayload {
                    AttackerIndex = 1,
                    TargetIndex = 0,
                    AttackID = "test.strike",
                    HitboxID = "primary",
                    AttackClass = AttackClass.Basic,
                    Damage = 5f,
                    Delivery = HitDelivery.DirectHit,
                    Origin = HitOrigin.Basic
                })).IsTrue();
                if (owned) {
                    AssertFloat(tubman.MomentumPips - pips).IsEqualApprox(1f, 0.0005f);
                    AssertThat(tubman.StoryShieldEffectId).IsEqual(StoryShieldEffect.ResonanceBarrier);
                    AssertThat(tubman.StoryShieldSource).IsEqual(StoryShieldEffect.FreedomLine);
                    AssertThat(tubman.StoryShieldCapacity)
                        .IsEqual(StoryDefenseRules.GrantedShieldCapacityShare * tubman.MaximumHP);
                } else {
                    AssertThat(tubman.MomentumPips).IsEqual(pips);
                    AssertThat(tubman.StoryShieldEffectId).IsEqual(StoryShieldEffect.None);
                }
            });
        }
    }

    /// <summary>The bare Pulse's own hit on a mob it reaches (0 for one out of its radius).</summary>
    private static int PulseDamage(PlayerController tesla, TeslaLorentzPulse pulse, EnemyController mob) =>
        tesla.GlobalPosition.DistanceTo(mob.GlobalPosition) <= pulse.PulseRadius
            ? (int)System.MathF.Round(tesla.StoryAbilityDamage(pulse.Data.BaseDamage, pulse.Data.AbilityID))
            : 0;

    private static void OpenRift(EinsteinRelativityRift rift) {
        AssertThat(rift.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && !rift.RiftActive; frame++) rift._PhysicsProcess(Step);
        AssertThat(rift.RiftActive).IsTrue();
    }

    /// <summary>Casts <paramref name="ability"/> and drives it to its end, without running the hero.</summary>
    private static void CastToTheEnd(BaseSpecial ability) {
        AssertThat(ability.TryExecute()).IsTrue();
        for (int frame = 0; frame < 240 && ability.IsExecuting; frame++) ability._PhysicsProcess(Step);
        AssertThat(ability.IsExecuting).IsFalse();
        // A ground wave outlives the cast by a tick or two.
        for (int frame = 0; frame < 30; frame++) ability._PhysicsProcess(Step);
    }

    /// <summary>The owner lands a hit on <paramref name="mob"/>: every turret of his fires, and the bolt flies in real frames.</summary>
    private static int LiveBolts(Node host) {
        int live = 0;
        foreach (Node child in host.GetChildren()) if (child is PlaceholderProjectile { IsLive: true }) live++;
        return live;
    }

    private static async Task FireAtAndWait(SceneTree tree, PlayerController owner, EnemyController mob) {
        OwnerLands(owner, mob);
        for (int frame = 0; frame < 30; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }
}
