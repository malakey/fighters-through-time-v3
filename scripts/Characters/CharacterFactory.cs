using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;

namespace FTT.Characters {

	public static class CharacterFactory {
		public struct CharacterVisual {
			public Color Body;
			public Color Accent;
			public Color Detail;
		}

		/// <summary>Neutral fallback for an ID carrying no authored colours.</summary>
		private static readonly CharacterVisual FallbackVisual = new() {
			Body = Colors.Gray, Accent = Colors.White, Detail = Colors.LightGray
		};

		/// <summary>
		/// Package 11 A6b (recon A §9). This was a nine-entry dictionary literal
		/// — one of five identical hardcoded rosters. The colours now live on
		/// each <see cref="CharacterData"/> resource
		/// (<c>PlaceholderBodyColor</c> / <c>Accent</c> / <c>Detail</c>), so a
		/// tenth character brings its identity colours with its own <c>.tres</c>
		/// and needs no C# edit. An unauthored (transparent) colour falls back to
		/// the neutral placeholder — exactly what an unknown ID produced before.
		/// </summary>
		public static CharacterVisual ResolveVisual(CharacterData data) {
			if (data == null) return FallbackVisual;
			return new CharacterVisual {
				Body = data.PlaceholderBodyColor.A > 0f ? data.PlaceholderBodyColor : FallbackVisual.Body,
				Accent = data.PlaceholderAccentColor.A > 0f ? data.PlaceholderAccentColor : FallbackVisual.Accent,
				Detail = data.PlaceholderDetailColor.A > 0f ? data.PlaceholderDetailColor : FallbackVisual.Detail
			};
		}

		/// <summary>
		/// This character's placeholder identity colour, read from its authored
		/// resource through <see cref="AuthoredResources"/> (cached for the
		/// process lifetime, so repeated presentation calls do not re-parse).
		/// </summary>
		public static Color GetCharacterColor(string characterID) {
			if (!IsKnownCharacter(characterID)) return FallbackVisual.Body;
			var data = AuthoredResources.Load<CharacterData>(
				$"res://resources/Characters/{characterID}_data.tres");
			return ResolveVisual(data).Body;
		}

		/// <summary>
		/// True when <paramref name="characterID"/> is a roster member this
		/// factory can build. Callers that interpolate the ID into a
		/// <c>res://resources/Characters/</c> path must gate on this first so an
		/// unknown or empty ID never fabricates a resource lookup.
		///
		/// <para>Package 11 A6b: the membership test is the content manifest
		/// (<see cref="FTT.Core.CharacterRoster"/>), not a literal cast list.</para>
		/// </summary>
		public static bool IsKnownCharacter(string characterID) =>
			CharacterRoster.Contains(characterID);

		/// <summary>
		/// Package 11 A5: the ability slots the active story save has restored
		/// for <paramref name="characterID"/>, or <c>null</c> when there is no
		/// active story save at all.
		///
		/// <para>Null means "install no gate". That is deliberate and is the
		/// correct reading for the Test Arena, a unit test, and the developer
		/// level select (which sets <c>ActiveSaveSlot = -1</c> precisely so no
		/// real campaign can be written): a debug launch straight into Level 9
		/// must not arrive with a Level-0 kit. A real campaign always has its
		/// slot, so the gate always installs where it matters.</para>
		/// </summary>
		public static HashSet<AbilitySlot> ResolveStoryUnlockedSlots(string characterID) {
			SaveManager saveManager = SaveManager.Instance;
			GameManager gameManager = GameManager.Instance;
			if (saveManager == null || gameManager == null) return null;
			int slotIndex = gameManager.CurrentSession.ActiveSaveSlot;
			if (slotIndex < 0 || slotIndex >= saveManager.SaveSlots.Length) return null;
			StorySaveData save = saveManager.SaveSlots[slotIndex];
			if (save == null) return null;

			var slots = new HashSet<AbilitySlot>();
			if (save.UnlockedLegacyAbilities != null
				&& save.UnlockedLegacyAbilities.TryGetValue(characterID, out List<string> keys)) {
				slots.UnionWith(LegacyUnlockSchedule.SlotsFromSavedKeys(keys));
			}
			// Backstop for a payload written before the schedule existed: the
			// completed-level history is the same source of truth the milestone
			// grant uses, so the two can never disagree.
			foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
				if (LegacyUnlockSchedule.IsUnlocked(slot, save.CompletedLevels)) slots.Add(slot);
			}
			return slots;
		}

		/// <param name="applyStoryProgression">
		/// Copies the active save's Resonance stat profile and purchased ability
		/// perks onto the built character. The <c>false</c> seam is the Story/Fighter
		/// isolation boundary (Fighter Mode, the Holodeck, the Calibration Drills).
		/// </param>
		/// <param name="applyLegacyUnlockLocks">
		/// Package 11 A7b (F20). Separates the V7.5 Legacy ability-slot gate from the
		/// perk copy, which used to be a single switch. The Mirror Paradox needs the
		/// two apart: on Hard it mirrors the player's <b>purchased grid perks</b>
		/// (MIRROR_PARADOX.md, "initialize the clone's copied perk set from the
		/// player's purchased/unlocked grid nodes"), while its core kit access stays
		/// "independent of Story ability locks at encounter time" on every difficulty
		/// — a half-unlocked campaign must still face a mirror with both Specials, its
		/// movement ability and its Ultimate. Every other caller leaves this at the
		/// default, so the gate installs exactly where it did before.
		/// </param>
		public static PlayerController CreateCharacter(
			string characterID,
			int playerIndex = 0,
			bool applyStoryProgression = true,
			bool applyLegacyUnlockLocks = true) {
			// Pinned, not GD.Load: a character .tres pulls in four AbilityData
			// sub-resources and its SpriteFrames, all C#-scripted. Loading and
			// dropping that graph on every spawn cycles a dozen script instances
			// through the .NET finalizer thread and corrupts the heap
			// (FTT.Core.AuthoredResources).
			CharacterData data = AuthoredResources.Load<CharacterData>(
				$"res://resources/Characters/{characterID}_data.tres");
			if (data == null) throw new InvalidOperationException($"Character data not found for '{characterID}'.");

			var player = new PlayerController {
				PlayerIndex = playerIndex,
				Data = data,
				CollisionLayer = CollisionLayers.BodyLayerForFighterSlot(playerIndex),
				CollisionMask = CollisionLayers.BodyMaskForFighterSlot(playerIndex),
				MotionMode = CharacterBody2D.MotionModeEnum.Grounded,
				UpDirection = Vector2.Up,
				FloorStopOnSlope = true
			};
			if (applyStoryProgression
				&& FTT.Environment.ResonanceProgression.TryResolveActive(characterID, out FTT.Environment.StoryStatProfile storyStats)) {
				player.StoryMaxHPBonus = storyStats.MaxHPBonus;
				player.StoryBlockChargeBonus = storyStats.BlockChargeBonus;
				player.StoryMoveSpeedMultiplier = storyStats.MoveSpeedMultiplier;
				player.StoryJumpForceMultiplier = storyStats.JumpForceMultiplier;
				player.StoryBasicDamageMultiplier = storyStats.BasicDamageMultiplier;
				player.StorySpecialDamageMultiplier = storyStats.SpecialDamageMultiplier;
				player.StoryCooldownMultiplier = storyStats.CooldownMultiplier;
				player.StoryAttackRangeMultiplier = storyStats.AttackRangeMultiplier;
				player.StoryComboSpeedMultiplier = storyStats.ComboSpeedMultiplier;
				player.StoryBlockRecoveryMultiplier = storyStats.BlockRecoveryMultiplier;
				player.StoryKnockbackMultiplier = storyStats.KnockbackMultiplier;
				player.StoryProjectileSpeedMultiplier = storyStats.ProjectileSpeedMultiplier;
				player.StoryProjectileDamageMultiplier = storyStats.ProjectileDamageMultiplier;
				player.StoryGlideSpeedMultiplier = storyStats.GlideSpeedMultiplier;
				player.StoryGlideDurationMultiplier = storyStats.GlideDurationMultiplier;
				player.StoryZoneRadiusMultiplier = storyStats.ZoneRadiusMultiplier;
				player.StoryZoneDurationMultiplier = storyStats.ZoneDurationMultiplier;
				player.StoryPersistentDurationMultiplier = storyStats.PersistentDurationMultiplier;
				player.StoryPersistentRangeMultiplier = storyStats.PersistentRangeMultiplier;
				player.StoryPersistentHealthMultiplier = storyStats.PersistentHealthMultiplier;
				player.StoryStatusDurationMultiplier = storyStats.StatusDurationMultiplier;
				player.StoryStatusIntensityMultiplier = storyStats.StatusIntensityMultiplier;
				// Package 11 A4 (Resonance V7.6): three new character-wide lanes
				// plus the one scoped bucket - a single assignment instead of
				// six-times-N hand-copied fields.
				player.StoryRallyEchoFractionMultiplier = storyStats.RallyEchoFractionMultiplier;
				player.StoryUltimateBuildRateMultiplier = storyStats.UltimateBuildRateMultiplier;
				player.StoryExtractorDamageMultiplier = storyStats.ExtractorDamageMultiplier;
				player.StoryScopedStats = new FTT.Environment.ScopedStoryStats(storyStats.ScopedMultipliers);
				FTT.Environment.ResonanceProgression.TryCollectActiveAbilityModifiers(
					characterID, player.StoryAbilityPerks);
			}
			// Package 11 A5 (V7.5 Legacy Unlock Schedule). Story-only: the
			// applyStoryProgression: false seam — Fighter Mode, the hub
			// Holodeck, the Calibration Drills and the Mirror Paradox clone —
			// never installs the gate, so those paths always run the full kit.
			if (applyStoryProgression && applyLegacyUnlockLocks) {
				HashSet<AbilitySlot> unlocked = ResolveStoryUnlockedSlots(characterID);
				if (unlocked != null) player.ApplyLegacyUnlockLocks(unlocked);
				// Package 11 A1b (V7.6 F10): Defy History is once per ATTEMPT.
				// The per-attempt authority lives on StoryManager, so a death
				// rewind or a mid-level resume that rebuilds the player seeds a
				// SPENT Defy rather than handing the run a second saved life.
				// The isolation seam is deliberate: Fighter Mode, the Holodeck,
				// the Calibration Drills and the Mirror Paradox clone always
				// start with an unused Defy.
				// Integration guard: the per-attempt authority only exists inside a
				// live campaign attempt. Outside one (the Test Arena, a headless
				// fixture that never ran BeginLevelRun) the singleton flag is stale
				// state from an earlier attempt and must not seed a spent Defy.
				var storyManager = FTT.Core.StoryManager.Instance;
				if (storyManager != null && storyManager.HasLiveAttempt
					&& storyManager.StoryDefyHistoryUsed) {
					player.SetStoryDefyHistoryUsed(true);
				}
			}

			var bodyShape = new CollisionShape2D { Name = "CollisionShape2D", Position = new Vector2(0, -32) };
			bodyShape.Shape = new RectangleShape2D { Size = new Vector2(40, 64) };
			player.AddChild(bodyShape);

			CharacterVisual visual = ResolveVisual(data);
			BuildVisual(player, visual, data, heroAura: applyStoryProgression);
			BuildMovementSensors(player);
			BuildHurtbox(player, playerIndex);
			BuildPushbox(player, playerIndex);
			BuildMeleeHitbox(player, playerIndex, data);
			BuildCombatAnimationPlayer(player);
			player.AddChild(new FTT.Environment.TemporalPositionHistory { Name = "TemporalPositionHistory" });

			player.AddChild(new UltimateMeter { Name = "UltimateMeter" });
			player.AddChild(new BlockSystem { Name = "BlockSystem", MaxCharges = player.MaximumBlockCharges });
			player.AddChild(new StatusController { Name = "StatusController" });
			SetupAbilities(player, characterID);
			player.AddToGroup("Players");
			return player;
		}

		private static void BuildVisual(PlayerController player, CharacterVisual visual, CharacterData data,
			bool heroAura) {
			SpriteFrames frames = data.SpriteFramesResource
				?? GD.Load<SpriteFrames>("res://resources/SpriteFrames/placeholder_character_frames.tres");
			var sprite = new AnimatedSprite2D {
				Name = "AnimatedSprite2D",
				SpriteFrames = frames,
				// 128 px frame at 0.845 scale spans 108.2 px; -54.08 keeps the drawn feet
				// on the body origin, which is what rests on the floor in both modes.
				// (0.845 is the second 2026-08-11 +30% size pass: 0.5 -> 0.65 -> 0.845.)
				Position = new Vector2(0, -54.08f),
				Scale = new Vector2(0.845f, 0.845f)
			};
			player.AddChild(sprite);
			if (frames != null && frames.HasAnimation("idle")) sprite.Play("idle");
			// Per-animation figure-scale correction for the generated sheets;
			// must attach after the idle base pose/scale is established.
			FTT.Combat.RetroSpriteScaleNormalizer.Attach(
				sprite, FTT.Combat.RetroSpriteScaleNormalizer.FigureKind.Character);
			// Package 8 A3: the gold ChronalArmorOverlay ColorRect is replaced by the
			// shader-driven hyper-armor shell on the shared glow arbiter, which also
			// owns status/spawn-invulnerability outlines and the sprite tint.
			FTT.Combat.GlowPresentationController glow =
				FTT.Combat.GlowPresentationController.AttachTo(
					player, sprite, player.PlayerIndex, subscribeToStoryEvents: true);
			// Package 11 A6b (design §2, the two-colour visual grammar): the
			// campaign hero carries a persistent warm-gold resonance aura for the
			// whole game, the counterpoint to every cold Unbound light in the
			// world. Story only — a Fighter proxy's identity is the F24 ownership
			// edge, and lighting both fighters gold would say "both of you are
			// the hero", which the grammar must never say. Story/Fighter
			// isolation is the same seam the Legacy Unlock gate uses above.
			if (glow != null) glow.SetHeroAura(heroAura);

			var label = new Label {
				Name = "NameLabel",
				Text = data.DisplayName,
				Position = new Vector2(-55, -84),
				CustomMinimumSize = new Vector2(110, 18),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", visual.Body);
			player.AddChild(label);
		}

		private static void BuildMovementSensors(PlayerController player) {
			var ledgeDetector = new Area2D {
				Name = "LedgeDetector",
				CollisionLayer = 0,
				CollisionMask = CollisionLayers.Trigger,
				Monitoring = true,
				Monitorable = false
			};
			ledgeDetector.AddChild(new CollisionShape2D {
				Position = new Vector2(0, -52),
				Shape = new RectangleShape2D { Size = new Vector2(56, 36) }
			});
			player.AddChild(ledgeDetector);
			player.AddChild(new Marker2D { Name = "AerialHitboxMarker", Position = new Vector2(56, -42) });
		}

		private static void BuildHurtbox(PlayerController player, int playerIndex) {
			var hurtbox = new Hurtbox {
				Name = "Hurtbox",
				OwnerPlayerIndex = playerIndex,
				CollisionLayer = CollisionLayers.HurtboxLayerForFighterSlot(playerIndex),
				CollisionMask = CollisionLayers.HurtboxMaskForFighterSlot(playerIndex),
				Monitorable = true,
				Monitoring = true
			};
			var shape = new CollisionShape2D { Name = "CollisionShape2D", Position = new Vector2(0, -32) };
			shape.Shape = new RectangleShape2D { Size = new Vector2(40, 64) };
			hurtbox.AddChild(shape);
			player.AddChild(hurtbox);
		}

		private static void BuildPushbox(PlayerController player, int playerIndex) {
			var pushbox = new CombatantPushbox {
				Name = "Pushbox",
				BoxSize = new Vector2(30f, 48f),
				Position = new Vector2(0f, -28f),
				CollisionLayer = CollisionLayers.BodyLayerForFighterSlot(playerIndex),
				CollisionMask = playerIndex == 0 ? CollisionLayers.Enemy : CollisionLayers.Player,
				Monitoring = false,
				Monitorable = false
			};
			var shape = new CollisionShape2D {
				Shape = new RectangleShape2D { Size = pushbox.BoxSize }
			};
			pushbox.AddChild(shape);
			player.AddChild(pushbox);
		}

		private static void BuildMeleeHitbox(PlayerController player, int playerIndex, CharacterData data) {
			var hitbox = new Hitbox {
				Name = "MeleeHitbox",
				AttackID = $"{data.CharacterID}.basic",
				HitboxID = "combo_1",
				AttackClass = AttackClass.Basic,
				Damage = data.BasicAttackDamage * player.StoryBasicDamageMultiplier,
				KnockbackForce = new Vector2(data.BasicAttackKnockback, -1.5f),
				HitstunDuration = 0.15f,
				OwnerPlayerIndex = playerIndex,
				CollisionLayer = CollisionLayers.HitboxLayerForFighterSlot(playerIndex),
				CollisionMask = CollisionLayers.HitboxMaskForFighterSlot(playerIndex),
				Monitorable = true,
				SourcePlayer = player
			};
			var shape = new CollisionShape2D { Name = "CollisionShape2D", Position = new Vector2(30, -32) };
			shape.Shape = new RectangleShape2D { Size = new Vector2(50, 45) };
			hitbox.AddChild(shape);
			player.AddChild(hitbox);
		}

		private static void BuildCombatAnimationPlayer(PlayerController player) {
			var animationPlayer = new AnimationPlayer {
				Name = "CombatAnimationPlayer",
				CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate
			};
			AnimationLibrary library = GD.Load<AnimationLibrary>(
				"res://resources/Animations/placeholder_combat_animation_library.tres");
			if (library != null) animationPlayer.AddAnimationLibrary("", library);
			player.AddChild(animationPlayer);
		}

		private static void SetupAbilities(PlayerController player, string characterID) {
			BaseSpecial[] implementations = characterID switch {
				"einstein" => new BaseSpecial[] { new EinsteinEmc2Blast(), new EinsteinRelativityRift(), new EinsteinRelativityWarp(), new EinsteinUltimate() },
				"joan" => new BaseSpecial[] { new JoanRighteousSmite(), new JoanDivinePiercing(), new JoanAscendantWings(), new JoanGrandCrusade() },
				"leonardo" => new BaseSpecial[] { new LeonardoGoldenRatio(), new LeonardoClockworkTurret(), new LeonardoOrnithopterFlight(), new LeonardoVitruvianMatrix() },
				"lincoln" => new BaseSpecial[] { new LincolnEmancipator(), new LincolnSplittingStrike(), new LincolnRailCharge(), new LincolnUnionIndestructible() },
				"cleopatra" => new BaseSpecial[] { new CleopatraSerpentNest(), new CleopatraSandstormVortex(), new CleopatraDesertMirage(), new CleopatraWrathOfTheNile() },
				"tesla" => new BaseSpecial[] { new TeslaTeslaCoil(), new TeslaLorentzPulse(), new TeslaLightningBlink(), new TeslaWardenclyffeCataclysm() },
				"shakespeare" => new BaseSpecial[] { new ShakespeareYoricksLament(), new ShakespeareTheTempest(), new ShakespeareProsperosFlight(), new ShakespeareAllTheWorldsAStage() },
				"mozart" => new BaseSpecial[] { new MozartRequiemChord(), new MozartFortissimoWave(), new MozartSonataDrift(), new MozartSymphonyOfSorrow() },
				"pocahontas" => new BaseSpecial[] { new PocahontasSpiritStrike(), new PocahontasVineSnare(), new PocahontasBreezeGlide(), new PocahontasTidewaterTempest() },
				_ => throw new InvalidOperationException($"No ability implementation map exists for '{characterID}'.")
			};

			AbilityData[] definitions = {
				player.Data.SpecialAttackOne,
				player.Data.SpecialAttackTwo,
				player.Data.MovementAbility,
				player.Data.UltimateAttack
			};
			string[] nodeNames = { "Special1", "Special2", "MovementAbility", "Ultimate" };

			for (int slot = 0; slot < implementations.Length; slot++) {
				AbilityData definition = definitions[slot]
					?? throw new InvalidOperationException($"'{characterID}' is missing ability data for slot {slot}.");
				BaseSpecial implementation = implementations[slot];
				implementation.Name = nodeNames[slot];
				implementation.Data = definition;
				foreach (string hitboxName in GetRequiredHitboxes(characterID, slot)) {
					implementation.AddChild(MakeHitbox(
						hitboxName, definition, player.PlayerIndex, player.StorySpecialDamageMultiplier));
				}
				player.AddChild(implementation);
			}
		}

		private static string[] GetRequiredHitboxes(string characterID, int slot) => (characterID, slot) switch {
			("einstein", 3) => new[] { "UltHitbox" },
			("joan", 3) => new[] { "CavalryHitbox" },
			("leonardo", 3) => new[] { "MatrixHitbox" },
			("lincoln", 1) => new[] { "OverheadHitbox" },
			("lincoln", 3) => new[] { "TrapHitbox", "SmashHitbox" },
			("tesla", 3) => new[] { "ShockwaveHitbox" },
			("shakespeare", 3) => new[] { "PhantomHitbox" },
			("pocahontas", 0) => new[] { "EagleHitbox" },
			("pocahontas", 3) => new[] { "StormHitbox" },
			_ => Array.Empty<string>()
		};

		private static Hitbox MakeHitbox(string name, AbilityData data, int ownerIndex, float damageMultiplier) {
			var hitbox = new Hitbox {
				Name = name,
				AttackID = data.AbilityID,
				HitboxID = name,
				Damage = data.BaseDamage * damageMultiplier,
				KnockbackForce = data.KnockbackForce,
				AppliedStatus = data.AppliedStatus,
				StatusDuration = data.StatusDuration,
				StatusIntensity = data.StatusIntensity,
				ScreenShakeIntensity = data.ScreenShakeIntensity,
				ScreenShakeDuration = data.ScreenShakeDuration,
				OwnerPlayerIndex = ownerIndex,
				CollisionLayer = CollisionLayers.HitboxLayerForFighterSlot(ownerIndex),
				CollisionMask = CollisionLayers.HitboxMaskForFighterSlot(ownerIndex),
				Monitorable = true
			};
			// M08 (Package 12 W3): class, hitstun, launch flag, delivery and
			// origin come from the ability's authored hit contract.
			hitbox.ApplyAbilityHitContract(data);
			var shape = new CollisionShape2D();
			shape.Shape = new RectangleShape2D { Size = data.HitboxSize };
			hitbox.AddChild(shape);
			return hitbox;
		}
	}
}
