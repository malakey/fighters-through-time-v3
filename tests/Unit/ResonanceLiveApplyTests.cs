using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-11 (grid-live-apply, ledger <c>FIX-GRID-LIVE-APPLY</c>): a Resonance
/// purchase or respec reaches the LIVE campaign hero at once ("stat modifiers are
/// applied immediately", design-godot.md, Resonance Grid UI Flow). Before it, the
/// only subscriber to the unlock event was the autosave and the factory resolved
/// the grid once at build, so a node bought at an Act III Warden Beacon never
/// reached that level's fights without a paid reload.
///
/// <para>Every case drives the real Repository screen from a real Beacon at an
/// activated Level 13 anchor, on a hero standing on a floor and stepped in real
/// physics frames: the purchase changes the hero's stats, scoped bucket and perk
/// set at once and its run speed on the next frames; a respec reverts them; a
/// respec and re-buy loop never heals; a purchase made mid-cast or mid-swing waits
/// for the end of that cast or swing, and the swing keeps its damage; the Legacy
/// locks never move; a raised block cap never refills the shield and a lowered one
/// clamps it; and only the campaign hero listens — never a Mirror clone or a
/// Fighter-path body.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceLiveApplyTests {

    private const string HeroID = "tubman";
    private const string MaxHPNode = "tubman_resolve";         // MaxHP +15
    private const string MoveSpeedNode = "tubman_stride";      // MoveSpeed +3 %
    private const string ScopedNode = "tubman_call_distance";  // AbilityRange +20 %, Conductor's Call
    private const string ScopedAbility = "tubman_conductors_call";
    private const string PerkNode = "tubman_star_guide";       // Traversal perk star_guide
    private const string PerkKey = "star_guide";
    private const string BasicDamageNode = "tubman_cane_edge";  // BasicAttackDamage +8 %
    private const string Layout = "Center/Panel/Layout/";

    [TestCase]
    public async Task BuyingAtABeaconChangesTheLiveHeroAtOnceAndARespecRevertsIt() {
        using var fixture = BeaconFixture.Open(save => save.UnlockedLegacyAbilities[HeroID] =
            new List<string> { LegacyUnlockSchedule.MovementKey, LegacyUnlockSchedule.SpecialOneKey });
        PlayerController hero = await fixture.SpawnHero();
        int baseHP = hero.Data.MaxHP;
        float baseSpeed = hero.Data.MaxMoveSpeed * 60f;
        AssertThat(hero.ReceivesLiveStoryProgression).IsTrue();
        AssertThat(hero.MaximumHP).IsEqual(baseHP);
        AssertThat(await fixture.RunSpeed()).IsEqualApprox(baseSpeed, 0.05f);

        ResonanceGridPanel panel = fixture.OpenRepository();
        fixture.Buy(panel, MaxHPNode);
        AssertThat(hero.MaximumHP)
            .OverrideFailureMessage("The MaxHP node must reach the hero the moment it is bought.")
            .IsEqual(baseHP + 15);
        AssertThat(hero.CurrentHP)
            .OverrideFailureMessage("A purchase raises the cap; it is not a heal.")
            .IsEqual(baseHP);
        AssertThat(fixture.LastHudMaxHP)
            .OverrideFailureMessage("The HUD hears the new maximum through the HP payload.")
            .IsEqual((float)(baseHP + 15));
        fixture.Buy(panel, MoveSpeedNode);
        fixture.Buy(panel, ScopedNode);
        fixture.Buy(panel, PerkNode);
        AssertThat(hero.StoryMoveSpeedMultiplier).IsEqualApprox(1.03f, 0.0001f);
        AssertThat(hero.StoryScoped("AbilityRange", ScopedAbility))
            .OverrideFailureMessage("The ability-scoped bucket is re-resolved with the rest.")
            .IsEqualApprox(1.2f, 0.0001f);
        AssertThat(hero.HasStoryPerk(PerkKey))
            .OverrideFailureMessage("The perk set is re-resolved with the rest.")
            .IsTrue();
        fixture.Close(panel);

        AssertThat(await fixture.RunSpeed())
            .OverrideFailureMessage("The live hero runs at the bought speed without a reload.")
            .IsEqualApprox(baseSpeed * 1.03f, 0.05f);
        AssertLegacyLocksUnchanged(hero);

        panel = fixture.OpenRepository();
        fixture.Respec(panel);
        AssertThat(hero.MaximumHP).IsEqual(baseHP);
        AssertThat(hero.CurrentHP).IsEqual(baseHP);
        AssertThat(hero.StoryMoveSpeedMultiplier).IsEqualApprox(1f, 0.0001f);
        AssertThat(hero.StoryScoped("AbilityRange", ScopedAbility)).IsEqualApprox(1f, 0.0001f);
        AssertThat(hero.HasStoryPerk(PerkKey))
            .OverrideFailureMessage("A respec drops every perk from the live hero.")
            .IsFalse();
        fixture.Close(panel);

        AssertThat(await fixture.RunSpeed())
            .OverrideFailureMessage("The respec reverts the live hero's speed at once.")
            .IsEqualApprox(baseSpeed, 0.05f);
        AssertLegacyLocksUnchanged(hero);
    }

    [TestCase]
    public async Task ARespecAndReBuyLoopNeverHeals() {
        using var fixture = BeaconFixture.Open(null);
        PlayerController hero = await fixture.SpawnHero();
        int baseHP = hero.Data.MaxHP;
        int blockCharges = hero.CurrentBlockCharges;

        // Wounded: three full respec / re-buy loops leave HP exactly where it was.
        hero.CurrentHP = baseHP - 30;
        for (int loop = 0; loop < 3; loop++) {
            ResonanceGridPanel panel = fixture.OpenRepository();
            fixture.Buy(panel, MaxHPNode);
            AssertThat(hero.MaximumHP).IsEqual(baseHP + 15);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage($"Loop {loop}: buying MaxHP healed the hero.")
                .IsEqual(baseHP - 30);
            fixture.Respec(panel);
            AssertThat(hero.MaximumHP).IsEqual(baseHP);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage($"Loop {loop}: the respec changed a wound under the new cap.")
                .IsEqual(baseHP - 30);
            fixture.Close(panel);
            await fixture.Frames(5);
            AssertThat(hero.CurrentHP).IsEqual(baseHP - 30);
        }

        // Topped up past the base cap: the respec clamps, and the re-buy never
        // hands the clamped points back.
        ResonanceGridPanel again = fixture.OpenRepository();
        fixture.Buy(again, MaxHPNode);
        fixture.Close(again);
        await fixture.Frames(2);
        hero.HealStory(1000);
        AssertThat(hero.CurrentHP).IsEqual(baseHP + 15);
        again = fixture.OpenRepository();
        fixture.Respec(again);
        AssertThat(hero.CurrentHP)
            .OverrideFailureMessage("A lower cap clamps current HP to it.")
            .IsEqual(baseHP);
        fixture.Buy(again, MaxHPNode);
        AssertThat(hero.MaximumHP).IsEqual(baseHP + 15);
        AssertThat(hero.CurrentHP)
            .OverrideFailureMessage("A re-buy after a respec restored the clamped HP: the loop healed.")
            .IsEqual(baseHP);
        fixture.Close(again);
        await fixture.Frames(5);
        AssertThat(hero.CurrentHP).IsEqual(baseHP);
        // No authored node grants block charges, so this only shows that the
        // HP loop leaves the shield alone; the block cap itself is pinned by
        // ABlockCapRaiseNeverRefillsTheShieldAndALowerCapClampsIt.
        AssertThat(hero.CurrentBlockCharges)
            .OverrideFailureMessage("An HP-only grid change must leave the shield alone.")
            .IsEqual(blockCharges);
    }

    [TestCase]
    public async Task ABlockCapRaiseNeverRefillsTheShieldAndALowerCapClampsIt() {
        using var fixture = BeaconFixture.Open(null);
        PlayerController hero = await fixture.SpawnHero();
        BlockSystem block = hero.GetNode<BlockSystem>("BlockSystem");
        int baseCap = hero.MaximumBlockCharges;
        AssertThat(block.MaxCharges).IsEqual(baseCap);
        AssertThat(block.CurrentCharges).IsEqual(baseCap);
        var payloads = new List<BlockChargesPayload>();
        Action<BlockChargesPayload> listener = payload => {
            if (payload.PlayerIndex == 0) payloads.Add(payload);
        };
        EventBus.Instance.OnBlockChargesChanged += listener;
        try {
            // No authored grid node grants block charges, so the lane is set by
            // hand and the settle a live re-resolve runs is called directly.
            hero.StoryBlockChargeBonus = 1;
            hero.SettleLiveStoryProgression();
            AssertThat(block.MaxCharges).IsEqual(baseCap + 1);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A raised block cap is room for the regen, not a refill.")
                .IsEqual(baseCap);
            AssertThat(hero.CurrentBlockCharges).IsEqual(baseCap);
            AssertThat(payloads.Count).IsEqual(1);
            AssertThat(payloads[0].MaxCharges).IsEqual(baseCap + 1);
            AssertThat(payloads[0].CurrentCharges).IsEqual(baseCap);

            payloads.Clear();
            hero.SettleLiveStoryProgression();
            AssertThat(payloads.Count)
                .OverrideFailureMessage("A settle that moves no cap must raise no block event.")
                .IsEqual(0);

            // Full at the raised cap, then lowered: the charges clamp to it.
            block.RestoreAllCharges();
            AssertThat(block.CurrentCharges).IsEqual(baseCap + 1);
            payloads.Clear();
            hero.StoryBlockChargeBonus = 0;
            hero.SettleLiveStoryProgression();
            AssertThat(block.MaxCharges).IsEqual(baseCap);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A lowered block cap clamps the charges to it.")
                .IsEqual(baseCap);
            AssertThat(hero.CurrentBlockCharges).IsEqual(baseCap);
            AssertThat(payloads.Count).IsEqual(1);
            AssertThat(payloads[0].MaxCharges).IsEqual(baseCap);
            AssertThat(payloads[0].CurrentCharges).IsEqual(baseCap);

            await fixture.Frames(5);
            AssertThat(hero.CurrentBlockCharges).IsEqual(baseCap);
            AssertThat(block.CurrentCharges).IsEqual(baseCap);
        } finally {
            EventBus.Instance.OnBlockChargesChanged -= listener;
        }
    }

    [TestCase]
    public async Task APurchaseMadeMidCastLandsWhenTheCastEnds() {
        using var fixture = BeaconFixture.Open(save => save.UnlockedLegacyAbilities[HeroID] =
            new List<string> { LegacyUnlockSchedule.SpecialTwoKey });
        PlayerController hero = await fixture.SpawnHero();
        int baseHP = hero.Data.MaxHP;
        BaseSpecial foresight = hero.GetNode<BaseSpecial>("Special2");
        AssertThat(foresight.TryExecute()).IsTrue();
        await fixture.Frames(2);
        AssertThat(foresight.IsExecuting).IsTrue();

        // The Repository opened over a running stance: the purchase is held.
        ResonanceGridPanel panel = fixture.OpenRepository();
        fixture.Buy(panel, MaxHPNode);
        AssertThat(hero.MaximumHP)
            .OverrideFailureMessage("A cast in progress keeps the parameters it started with.")
            .IsEqual(baseHP);
        AssertThat(hero.LiveStoryProgressionPending).IsTrue();
        fixture.Close(panel);

        int frames = 0;
        while (foresight.IsExecuting && frames < 240) {
            AssertThat(hero.MaximumHP).IsEqual(baseHP);
            await fixture.Frames(1);
            frames++;
        }
        AssertThat(foresight.IsExecuting).OverrideFailureMessage("Foresight never ended.").IsFalse();
        await fixture.Frames(1);
        AssertThat(hero.LiveStoryProgressionPending).IsFalse();
        AssertThat(hero.MaximumHP)
            .OverrideFailureMessage("The held purchase lands on the first tick after the cast.")
            .IsEqual(baseHP + 15);
        AssertThat(hero.CurrentHP).IsEqual(baseHP);
    }

    [TestCase]
    public async Task APurchaseMadeMidSwingKeepsTheSwingsDamageAndLandsAfterIt() {
        using var fixture = BeaconFixture.Open(null);
        PlayerController hero = await fixture.SpawnHero();
        Hitbox melee = hero.GetNode<Hitbox>("MeleeHitbox");
        BasicStringProfile profile = BasicComboRules.StringProfileFor(HeroID);
        float baseDamage = hero.Data.BasicAttackDamage;
        float finisherDamage = baseDamage * profile.DamageTenths[2] / 10f;
        AssertThat(Mathf.IsEqualApprox(finisherDamage, baseDamage))
            .OverrideFailureMessage("The finisher must not deal the flat basic damage, or this pin proves nothing.")
            .IsFalse();

        // A press every tick runs the string; let go once the finisher starts.
        fixture.Press(GameplayButtons.BasicAttack);
        for (int frame = 0; frame < 240
            && !(hero.CurrentState == CharacterState.Attacking && hero.ComboCounter == 2); frame++) {
            await fixture.Frames(1);
        }
        fixture.Hold(0f);
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Attacking);
        AssertThat(hero.ComboCounter).OverrideFailureMessage("The string never reached its finisher.").IsEqual(2);
        AssertThat(melee.IsActive)
            .OverrideFailureMessage("The Beacon must open inside the finisher's startup.")
            .IsFalse();
        AssertThat(melee.Damage).IsEqualApprox(finisherDamage, 0.001f);

        // Opened inside the startup: the purchase is held and the inactive box
        // keeps the damage the finisher wrote when it started.
        ResonanceGridPanel panel = fixture.OpenRepository();
        fixture.Buy(panel, BasicDamageNode);
        AssertThat(hero.LiveStoryProgressionPending).IsTrue();
        AssertThat(hero.StoryBasicDamageMultiplier).IsEqualApprox(1f, 0.0001f);
        AssertThat(melee.Damage)
            .OverrideFailureMessage("The purchase rewrote the damage of a finisher still in its startup.")
            .IsEqualApprox(finisherDamage, 0.001f);
        fixture.Close(panel);

        bool sawActive = false;
        int frames = 0;
        while (hero.CurrentState == CharacterState.Attacking && frames < 120) {
            AssertThat(hero.StoryBasicDamageMultiplier)
                .OverrideFailureMessage("A swing in progress keeps the grid it started with.")
                .IsEqualApprox(1f, 0.0001f);
            if (melee.IsActive) {
                sawActive = true;
                AssertThat(melee.CreatePayload(1).Damage)
                    .OverrideFailureMessage("The finisher must land at its own damage, not the flat basic value.")
                    .IsEqualApprox(finisherDamage, 0.001f);
            }
            await fixture.Frames(1);
            frames++;
        }
        AssertThat(sawActive).OverrideFailureMessage("The finisher never swung.").IsTrue();
        AssertThat(hero.CurrentState).IsNotEqual(CharacterState.Attacking);
        await fixture.Frames(1);
        AssertThat(hero.LiveStoryProgressionPending).IsFalse();
        AssertThat(hero.StoryBasicDamageMultiplier)
            .OverrideFailureMessage("The held purchase lands on the first tick after the swing.")
            .IsEqualApprox(1.08f, 0.0001f);

        // The next swing reads the bought lane.
        fixture.Press(GameplayButtons.BasicAttack);
        for (int frame = 0; frame < 30 && hero.CurrentState != CharacterState.Attacking; frame++) {
            await fixture.Frames(1);
        }
        fixture.Hold(0f);
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Attacking);
        AssertThat(hero.ComboCounter).IsEqual(0);
        AssertThat(melee.Damage)
            .OverrideFailureMessage("The next swing must carry the bought basic damage.")
            .IsEqualApprox(baseDamage * 1.08f * profile.DamageTenths[0] / 10f, 0.001f);
    }

    [TestCase]
    public async Task OnlyTheCampaignHeroReResolves() {
        using var fixture = BeaconFixture.Open(null);
        PlayerController hero = await fixture.SpawnHero();
        int baseHP = hero.Data.MaxHP;
        // The Hard Mirror clone's build (perks mirrored, the gate never
        // installed) and a Fighter-path body (no Story progression at all).
        PlayerController clone = fixture.Track(CharacterFactory.CreateCharacter(
            HeroID, 1, applyStoryProgression: true, applyLegacyUnlockLocks: false));
        clone.IsStoryHostile = true;
        PlayerController fighter = fixture.Track(CharacterFactory.CreateCharacter(
            HeroID, 1, applyStoryProgression: false));
        AssertThat(clone.ReceivesLiveStoryProgression).IsFalse();
        AssertThat(fighter.ReceivesLiveStoryProgression).IsFalse();
        clone.RemoveFromGroup("StoryPlayer");
        fighter.RemoveFromGroup("StoryPlayer");

        ResonanceGridPanel panel = fixture.OpenRepository();
        fixture.Buy(panel, MaxHPNode);
        fixture.Buy(panel, MoveSpeedNode);
        fixture.Close(panel);
        await fixture.Frames(2);

        AssertThat(hero.MaximumHP).IsEqual(baseHP + 15);
        AssertThat(clone.MaximumHP)
            .OverrideFailureMessage("The Mirror clone keeps the grid it was built with.")
            .IsEqual(baseHP);
        AssertThat(clone.StoryMoveSpeedMultiplier).IsEqualApprox(1f, 0.0001f);
        AssertThat(fighter.MaximumHP).IsEqual(baseHP);
        AssertThat(fighter.StoryMoveSpeedMultiplier).IsEqualApprox(1f, 0.0001f);
        // A direct call refuses both too.
        AssertThat(CharacterFactory.ReapplyStoryProgression(clone)).IsFalse();
        AssertThat(CharacterFactory.ReapplyStoryProgression(fighter)).IsFalse();
        AssertThat(clone.MaximumHP).IsEqual(baseHP);
        AssertThat(fighter.MaximumHP).IsEqual(baseHP);
    }

    private static void AssertLegacyLocksUnchanged(PlayerController hero) {
        AssertThat(hero.IsAbilityUnlocked(AbilitySlot.MovementAbility)).IsTrue();
        AssertThat(hero.IsAbilityUnlocked(AbilitySlot.Special1)).IsTrue();
        AssertThat(hero.IsAbilityUnlocked(AbilitySlot.Special2))
            .OverrideFailureMessage("A grid change must never touch the Legacy locks.")
            .IsFalse();
        AssertThat(hero.IsAbilityUnlocked(AbilitySlot.Ultimate)).IsFalse();
    }

    /// <summary>
    /// A Level 13 attempt on a scratch slot with 1000 deposited dust, a floor, a
    /// Beacon at the activated entry anchor and (once spawned) the campaign hero
    /// built from that slot. Disposing frees every node it added and restores the
    /// slot, the session and the Story state.
    /// </summary>
    private sealed class BeaconFixture : IDisposable {
        private const int ScratchSlot = 2;
        private const string CheckpointID = "level_13_chronal_void_checkpoint_0";

        private readonly SceneTree _tree = (SceneTree)Engine.GetMainLoop();
        private readonly StoryManager _story = StoryManager.Instance;
        private readonly SaveManager _saves = SaveManager.Instance;
        private readonly StorySaveData _originalSave;
        private readonly SessionData _originalSession;
        private readonly CampaignLevel _originalLevel;
        private readonly BufferedInputSource _input = new();
        private readonly List<Node> _owned = new();
        private readonly Action<PlayerHPPayload> _hpListener;
        private WardenBeacon _beacon;

        public PlayerController Hero { get; private set; }
        public float LastHudMaxHP { get; private set; } = -1f;

        private BeaconFixture() {
            _originalSave = _saves.SaveSlots[ScratchSlot];
            _originalSession = GameManager.Instance.CurrentSession;
            _originalLevel = _story.CurrentLevel;
            _hpListener = payload => { if (payload.PlayerIndex == 0) LastHudMaxHP = payload.MaxHP; };
            EventBus.Instance.OnPlayerHPChanged += _hpListener;
        }

        public static BeaconFixture Open(Action<StorySaveData> configure) {
            var fixture = new BeaconFixture();
            try {
                fixture.Setup(configure);
            } catch {
                fixture.Dispose();
                throw;
            }
            return fixture;
        }

        private void Setup(Action<StorySaveData> configure) {
            var save = new StorySaveData { SelectedCharacterID = HeroID };
            save.DepositedChronalDust[HeroID] = 1000;
            configure?.Invoke(save);
            _saves.SaveSlots[ScratchSlot] = save;

            _story.SuppressSceneLoadsForTesting = true;
            _story.PrepareDirectLevel(CampaignLevel.ChronalVoid, HeroID, Difficulty.Normal);
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;
            _story.BeginLevelRun();
            _story.TryActivateCheckpoint(CheckpointID);

            var floor = new StaticBody2D {
                Name = "LiveResonanceFloor",
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
                Position = new Vector2(0f, 20f)
            });
            Track(floor);
            _beacon = Track(WardenBeacon.Create(CheckpointID, new Vector2(-1200f, 0f)));
        }

        /// <summary>Adds a node to the root and owns its teardown.</summary>
        public T Track<T>(T node) where T : Node {
            _owned.Add(node);
            _tree.Root.AddChild(node);
            return node;
        }

        /// <summary>The campaign hero, built from the active slot and settled on the floor.</summary>
        public async Task<PlayerController> SpawnHero() {
            Hero = Track(CharacterFactory.CreateCharacter(HeroID, 0));
            Hero.GlobalPosition = new Vector2(0f, -8f);
            InputManager.Instance.SetInputSource(0, _input);
            Hold(0f);
            for (int frame = 0; frame < 60 && !(Hero.IsOnFloor() && Hero.CurrentState == CharacterState.Idle); frame++) {
                await Frames(1);
            }
            AssertThat(Hero.IsOnFloor()).OverrideFailureMessage("The hero never landed.").IsTrue();
            return Hero;
        }

        public void Hold(float moveX) =>
            _input.SetNextFrame(PlayerInputFrame.Create(0, moveX, 0f, GameplayButtons.None));

        /// <summary>Presses (and holds) <paramref name="buttons"/> on every following frame.</summary>
        public void Press(GameplayButtons buttons) =>
            _input.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));

        public async Task Frames(int count) {
            for (int frame = 0; frame < count; frame++) {
                await _tree.ToSignal(_tree, SceneTree.SignalName.PhysicsFrame);
            }
        }

        /// <summary>Holds right for 12 real frames, reads the run speed, then stops.</summary>
        public async Task<float> RunSpeed() {
            Hold(1f);
            await Frames(12);
            float speed = Hero.Velocity.X;
            Hold(0f);
            await Frames(10);
            return speed;
        }

        public ResonanceGridPanel OpenRepository() {
            _beacon.Interact(Hero);
            AssertThat(_beacon.OpenPanel != null)
                .OverrideFailureMessage("The Beacon did not open the Repository.")
                .IsTrue();
            return _beacon.OpenPanel;
        }

        public void Buy(ResonanceGridPanel panel, string nodeID) {
            panel.Root.GetNode<Control>(Layout + "NodeGrid").GetNode<Button>(nodeID)
                .EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(panel.Confirmation.IsOpen)
                .OverrideFailureMessage($"{nodeID} was not purchasable.")
                .IsTrue();
            panel.Confirmation.Confirm();
            AssertThat(_saves.SaveSlots[ScratchSlot].GridProgress[HeroID].Contains(nodeID))
                .IsTrue();
        }

        public void Respec(ResonanceGridPanel panel) {
            panel.RespecButton.EmitSignal(BaseButton.SignalName.Pressed);
            panel.RespecConfirmation.Confirm();
            AssertThat(_saves.SaveSlots[ScratchSlot].GridProgress[HeroID].Count).IsEqual(0);
        }

        public void Close(ResonanceGridPanel panel) {
            panel.Root.GetNode<Button>(Layout + "CloseButton").EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(Hero.ProcessMode).IsEqual(Node.ProcessModeEnum.Inherit);
        }

        public void Dispose() {
            EventBus.Instance.OnPlayerHPChanged -= _hpListener;
            InputManager.Instance?.ClearInputSource(0);
            for (int index = _owned.Count - 1; index >= 0; index--) {
                Node node = _owned[index];
                if (node == null || !GodotObject.IsInstanceValid(node)) continue;
                node.GetParent()?.RemoveChild(node);
                node.Free();
            }
            _owned.Clear();
            _story.SuppressSceneLoadsForTesting = false;
            _story.PrepareDirectLevel(_originalLevel, _originalSession.SelectedCharacterID ?? "einstein",
                _originalSession.Difficulty);
            _story.ClearLevelAttemptState();
            _story.StopLevelRun();
            _saves.SaveSlots[ScratchSlot] = _originalSave;
            if (_originalSave == null) _saves.DeleteStorySlot(ScratchSlot);
            else _saves.SaveStorySlot(ScratchSlot);
            GameManager.Instance.CurrentSession = _originalSession;
        }
    }
}
