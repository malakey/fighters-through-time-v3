using Godot;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Characters.Abilities;

namespace FTT.Characters {

	public static class CharacterFactory {

		public struct CharacterVisual {
			public Color Body;
			public Color Accent;
			public Color Detail;
		}

		private static readonly Dictionary<string, CharacterVisual> Visuals = new() {
			{ "einstein",    new() { Body = new Color(0.2f, 0.5f, 0.9f),  Accent = new Color(0.9f, 0.9f, 0.7f), Detail = new Color(0.6f, 0.6f, 0.6f) } },
			{ "joan",        new() { Body = new Color(0.85f, 0.75f, 0.2f), Accent = new Color(0.7f, 0.7f, 0.75f), Detail = new Color(0.9f, 0.2f, 0.1f) } },
			{ "leonardo",    new() { Body = new Color(0.3f, 0.7f, 0.3f),  Accent = new Color(0.6f, 0.4f, 0.2f), Detail = new Color(0.8f, 0.7f, 0.5f) } },
			{ "lincoln",     new() { Body = new Color(0.15f, 0.15f, 0.35f), Accent = new Color(0.3f, 0.3f, 0.3f), Detail = new Color(0.9f, 0.85f, 0.7f) } },
			{ "cleopatra",   new() { Body = new Color(0.6f, 0.2f, 0.8f),  Accent = new Color(0.9f, 0.75f, 0.2f), Detail = new Color(0.3f, 0.8f, 0.6f) } },
			{ "tesla",       new() { Body = new Color(0.1f, 0.8f, 0.9f),  Accent = new Color(0.3f, 0.3f, 0.4f), Detail = new Color(0.9f, 0.9f, 0.2f) } },
			{ "shakespeare", new() { Body = new Color(0.7f, 0.15f, 0.2f), Accent = new Color(0.9f, 0.85f, 0.7f), Detail = new Color(0.4f, 0.2f, 0.5f) } },
			{ "mozart",      new() { Body = new Color(0.9f, 0.85f, 0.8f), Accent = new Color(0.5f, 0.3f, 0.6f), Detail = new Color(0.8f, 0.6f, 0.7f) } },
			{ "pocahontas",  new() { Body = new Color(0.55f, 0.35f, 0.2f), Accent = new Color(0.3f, 0.7f, 0.4f), Detail = new Color(0.8f, 0.6f, 0.3f) } },
		};

		public static Color GetCharacterColor(string characterID) {
			return Visuals.TryGetValue(characterID, out var v) ? v.Body : Colors.Gray;
		}

		public static PlayerController CreateCharacter(string characterID, int playerIndex = 0) {
			var player = new PlayerController();
			player.PlayerIndex = playerIndex;

			var data = GD.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
			player.Data = data;

			player.CollisionLayer = 2;
			player.CollisionMask = 1 | 2;
			player.MotionMode = CharacterBody2D.MotionModeEnum.Grounded;
			player.UpDirection = Vector2.Up;
			player.FloorStopOnSlope = true;

			var col = new CollisionShape2D();
			col.Name = "CollisionShape2D";
			var colRect = new RectangleShape2D();
			colRect.Size = new Vector2(40, 64);
			col.Shape = colRect;
			col.Position = new Vector2(0, -32);
			player.AddChild(col);

			var vis = Visuals.GetValueOrDefault(characterID,
				new CharacterVisual { Body = Colors.Gray, Accent = Colors.White, Detail = Colors.LightGray });
			BuildVisual(player, vis, data?.DisplayName ?? characterID);

			BuildHurtbox(player, playerIndex);
			BuildMeleeHitbox(player, playerIndex, data);

			var meter = new UltimateMeter();
			meter.Name = "UltimateMeter";
			player.AddChild(meter);

			var block = new BlockSystem();
			block.Name = "BlockSystem";
			block.MaxCharges = data?.MaxBlockCharges ?? 3;
			player.AddChild(block);

			var status = new StatusController();
			status.Name = "StatusController";
			player.AddChild(status);

			SetupAbilities(player, characterID, playerIndex);

			player.AddToGroup("Players");
			return player;
		}

		private static void BuildVisual(PlayerController player, CharacterVisual vis, string displayName) {
			var body = new ColorRect();
			body.Name = "PlaceholderBody";
			body.Size = new Vector2(40, 64);
			body.Position = new Vector2(-20, -64);
			body.Color = vis.Body;
			player.AddChild(body);

			var belt = new ColorRect();
			belt.Size = new Vector2(40, 6);
			belt.Position = new Vector2(-20, -38);
			belt.Color = vis.Detail;
			player.AddChild(belt);

			var head = new ColorRect();
			head.Name = "HeadRect";
			head.Size = new Vector2(24, 18);
			head.Position = new Vector2(-12, -82);
			head.Color = vis.Accent;
			player.AddChild(head);

			var eyes = new ColorRect();
			eyes.Size = new Vector2(14, 4);
			eyes.Position = new Vector2(-4, -78);
			eyes.Color = new Color(0.1f, 0.1f, 0.1f);
			player.AddChild(eyes);

			var label = new Label();
			label.Name = "NameLabel";
			label.Text = displayName;
			label.Position = new Vector2(-55, -102);
			label.CustomMinimumSize = new Vector2(110, 18);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", vis.Body);
			player.AddChild(label);
		}

		private static void BuildHurtbox(PlayerController player, int playerIndex) {
			var hurtbox = new Hurtbox();
			hurtbox.Name = "Hurtbox";
			hurtbox.OwnerPlayerIndex = playerIndex;
			hurtbox.CollisionLayer = 8;
			hurtbox.CollisionMask = 4;
			hurtbox.Monitorable = true;
			hurtbox.Monitoring = true;
			var shape = new CollisionShape2D();
			var rect = new RectangleShape2D();
			rect.Size = new Vector2(40, 64);
			shape.Shape = rect;
			shape.Position = new Vector2(0, -32);
			hurtbox.AddChild(shape);
			player.AddChild(hurtbox);
		}

		private static void BuildMeleeHitbox(PlayerController player, int playerIndex, CharacterData data) {
			var hitbox = new Hitbox();
			hitbox.Name = "MeleeHitbox";
			hitbox.Damage = data?.BasicAttackDamage ?? 10f;
			hitbox.KnockbackForce = new Vector2(data?.BasicAttackKnockback ?? 3f, -1.5f);
			hitbox.HitstunDuration = 0.15f;
			hitbox.OwnerPlayerIndex = playerIndex;
			hitbox.CollisionLayer = 4;
			hitbox.CollisionMask = 8;
			hitbox.Monitorable = true;
			var shape = new CollisionShape2D();
			var rect = new RectangleShape2D();
			rect.Size = new Vector2(50, 45);
			shape.Shape = rect;
			shape.Position = new Vector2(30, -32);
			hitbox.AddChild(shape);
			player.AddChild(hitbox);

			var hitVisual = new ColorRect();
			hitVisual.Name = "MeleeHitVisual";
			hitVisual.Size = new Vector2(50, 45);
			hitVisual.Position = new Vector2(5, -55);
			hitVisual.Color = new Color(1, 1, 0.3f, 0.0f);
			player.AddChild(hitVisual);
		}

		private static AbilityData MakeAbilityData(string name, float damage, float cooldown,
			Vector2 knockback, float projSpeed = 0, FTT.Core.StatusType status = FTT.Core.StatusType.None,
			float statusDuration = 0, Vector2 hitboxSize = default) {
			var d = new AbilityData();
			d.AbilityName = name;
			d.BaseDamage = damage;
			d.CooldownDuration = cooldown;
			d.KnockbackForce = knockback;
			d.ProjectileSpeed = projSpeed;
			d.AppliedStatus = status;
			d.StatusDuration = statusDuration;
			d.HitboxSize = hitboxSize == default ? new Vector2(40, 40) : hitboxSize;
			d.HitboxOffset = new Vector2(30, 0);
			return d;
		}

		private static Hitbox MakeHitbox(string name, float damage, Vector2 knockback, Vector2 size, int ownerIndex) {
			var hb = new Hitbox();
			hb.Name = name;
			hb.Damage = damage;
			hb.KnockbackForce = knockback;
			hb.HitstunDuration = 0.2f;
			hb.OwnerPlayerIndex = ownerIndex;
			hb.CollisionLayer = 4;
			hb.CollisionMask = 8;
			hb.Monitorable = true;
			var shape = new CollisionShape2D();
			var rect = new RectangleShape2D();
			rect.Size = size;
			shape.Shape = rect;
			hb.AddChild(shape);
			return hb;
		}

		private static void SetupAbilities(PlayerController player, string characterID, int pi) {
			switch (characterID) {
				case "einstein":    SetupEinstein(player, pi); break;
				case "joan":        SetupJoan(player, pi); break;
				case "leonardo":    SetupLeonardo(player, pi); break;
				case "lincoln":     SetupLincoln(player, pi); break;
				case "cleopatra":   SetupCleopatra(player, pi); break;
				case "tesla":       SetupTesla(player, pi); break;
				case "shakespeare": SetupShakespeare(player, pi); break;
				case "mozart":      SetupMozart(player, pi); break;
				case "pocahontas":  SetupPocahontas(player, pi); break;
			}
		}

		private static void SetupEinstein(PlayerController p, int pi) {
			var s1 = new EinsteinEmc2Blast();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("E=mc² Blast", 15, 6, new Vector2(4, -2), projSpeed: 350);
			p.AddChild(s1);

			var s2 = new EinsteinRelativityRift();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Relativity Rift", 5, 10, new Vector2(2, -1), statusDuration: 3);
			p.AddChild(s2);

			var mv = new EinsteinRelativityWarp();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Relativity Warp", 0, 5, Vector2.Zero);
			p.AddChild(mv);

			var ult = new EinsteinUltimate();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Unified Field Theory", 15, 0, new Vector2(6, -4));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 5;
			ult.AddChild(MakeHitbox("UltHitbox", 15, new Vector2(6, -4), new Vector2(120, 80), pi));
			p.AddChild(ult);
		}

		private static void SetupJoan(PlayerController p, int pi) {
			var s1 = new JoanRighteousSmite();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Righteous Smite", 18, 7, new Vector2(5, -2), projSpeed: 250);
			p.AddChild(s1);

			var s2 = new JoanDivinePiercing();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Divine Piercing", 14, 8, new Vector2(4, -1), hitboxSize: new Vector2(60, 30));
			s2.AddChild(MakeHitbox("ThrustHitbox", 14, new Vector2(4, -1), new Vector2(60, 30), pi));
			p.AddChild(s2);

			var mv = new JoanAscendantWings();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Ascendant Wings", 0, 6, Vector2.Zero);
			p.AddChild(mv);

			var ult = new JoanGrandCrusade();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Grand Crusade", 12, 0, new Vector2(5, -3));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 6;
			ult.AddChild(MakeHitbox("CavalryHitbox", 12, new Vector2(5, -3), new Vector2(80, 60), pi));
			p.AddChild(ult);
		}

		private static void SetupLeonardo(PlayerController p, int pi) {
			var s1 = new LeonardoGoldenRatio();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Golden Ratio Spiral", 10, 8, new Vector2(3, -2));
			s1.Data.IsMultiHit = true;
			s1.Data.HitCount = 3;
			s1.AddChild(MakeHitbox("SpiralHitbox", 10, new Vector2(3, -2), new Vector2(80, 80), pi));
			p.AddChild(s1);

			var s2 = new LeonardoClockworkTurret();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Clockwork Turret", 8, 12, new Vector2(2, -1), projSpeed: 300);
			p.AddChild(s2);

			var mv = new LeonardoOrnithopterFlight();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Ornithopter Flight", 0, 6, Vector2.Zero);
			p.AddChild(mv);

			var ult = new LeonardoVitruvianMatrix();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Vitruvian Matrix", 12, 0, new Vector2(5, -3));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 8;
			ult.AddChild(MakeHitbox("MatrixHitbox", 12, new Vector2(5, -3), new Vector2(100, 70), pi));
			p.AddChild(ult);
		}

		private static void SetupLincoln(PlayerController p, int pi) {
			var s1 = new LincolnEmancipator();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Emancipator", 20, 7, new Vector2(6, -2), projSpeed: 200);
			p.AddChild(s1);

			var s2 = new LincolnSplittingStrike();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Splitting Strike", 22, 9, new Vector2(4, 5), hitboxSize: new Vector2(60, 50));
			s2.AddChild(MakeHitbox("OverheadHitbox", 22, new Vector2(4, 5), new Vector2(60, 50), pi));
			p.AddChild(s2);

			var mv = new LincolnRailCharge();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Rail Charge", 5, 7, new Vector2(3, -1));
			mv.Data.GrantsHyperArmor = true;
			p.AddChild(mv);

			var ult = new LincolnUnionIndestructible();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Union Indestructible", 25, 0, new Vector2(8, -5));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 5;
			ult.AddChild(MakeHitbox("TrapHitbox", 8, new Vector2(2, -1), new Vector2(80, 60), pi));
			ult.AddChild(MakeHitbox("SmashHitbox", 25, new Vector2(8, -5), new Vector2(90, 70), pi));
			p.AddChild(ult);
		}

		private static void SetupCleopatra(PlayerController p, int pi) {
			var s1 = new CleopatraSerpentNest();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Serpent Nest", 10, 10, new Vector2(2, -1),
				status: FTT.Core.StatusType.Stunned, statusDuration: 1f);
			p.AddChild(s1);

			var s2 = new CleopatraSandstormVortex();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Sandstorm Vortex", 6, 9, new Vector2(1, -1),
				status: FTT.Core.StatusType.TimeDilation, statusDuration: 2f);
			p.AddChild(s2);

			var mv = new CleopatraDesertMirage();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Desert Mirage", 0, 5, Vector2.Zero);
			p.AddChild(mv);

			var ult = new CleopatraWrathOfTheNile();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Wrath of the Nile", 12, 0, new Vector2(4, -3),
				status: FTT.Core.StatusType.Burning, statusDuration: 3f);
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 10;
			ult.AddChild(MakeHitbox("SandstormHitbox", 12, new Vector2(4, -3), new Vector2(100, 80), pi));
			p.AddChild(ult);
		}

		private static void SetupTesla(PlayerController p, int pi) {
			var s1 = new TeslaTeslaCoil();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Tesla Coil", 8, 10, new Vector2(2, -1));
			p.AddChild(s1);

			var s2 = new TeslaLorentzPulse();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Lorentz Pulse", 12, 8, new Vector2(3, -2), hitboxSize: new Vector2(80, 80));
			s2.AddChild(MakeHitbox("PulseHitbox", 12, new Vector2(3, -2), new Vector2(80, 80), pi));
			p.AddChild(s2);

			var mv = new TeslaLightningBlink();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Lightning Blink", 0, 4, Vector2.Zero);
			p.AddChild(mv);

			var ult = new TeslaWardenclyffeCataclysm();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Wardenclyffe Cataclysm", 18, 0, new Vector2(6, -4));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 4;
			ult.AddChild(MakeHitbox("ShockwaveHitbox", 18, new Vector2(6, -4), new Vector2(120, 90), pi));
			p.AddChild(ult);
		}

		private static void SetupShakespeare(PlayerController p, int pi) {
			var s1 = new ShakespeareYoricksLament();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Yorick's Lament", 14, 7, new Vector2(3, -2), projSpeed: 200);
			p.AddChild(s1);

			var s2 = new ShakespeareTheTempest();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("The Tempest", 0, 9, new Vector2(4, -2));
			p.AddChild(s2);

			var mv = new ShakespeareProsperosFlight();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Prospero's Flight", 0, 5, Vector2.Zero);
			p.AddChild(mv);

			var ult = new ShakespeareAllTheWorldsAStage();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("All The World's A Stage", 14, 0, new Vector2(5, -3));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 7;
			ult.AddChild(MakeHitbox("PhantomHitbox", 14, new Vector2(5, -3), new Vector2(70, 60), pi));
			p.AddChild(ult);
		}

		private static void SetupMozart(PlayerController p, int pi) {
			var s1 = new MozartRequiemChord();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Requiem Chord", 12, 6, new Vector2(3, -2), projSpeed: 280);
			p.AddChild(s1);

			var s2 = new MozartFortissimoWave();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Fortissimo Wave", 14, 8, new Vector2(4, -2), projSpeed: 250);
			p.AddChild(s2);

			var mv = new MozartSonataDrift();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Sonata Drift", 0, 6, Vector2.Zero);
			p.AddChild(mv);

			var ult = new MozartSymphonyOfSorrow();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Symphony of Sorrow", 15, 0, new Vector2(5, -4));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 10;
			p.AddChild(ult);
		}

		private static void SetupPocahontas(PlayerController p, int pi) {
			var s1 = new PocahontasSpiritStrike();
			s1.Name = "Special1";
			s1.Data = MakeAbilityData("Spirit Strike", 16, 7, new Vector2(4, -3));
			s1.AddChild(MakeHitbox("EagleHitbox", 16, new Vector2(4, -3), new Vector2(60, 50), pi));
			p.AddChild(s1);

			var s2 = new PocahontasVineSnare();
			s2.Name = "Special2";
			s2.Data = MakeAbilityData("Vine Snare", 8, 9, new Vector2(1, -1),
				status: FTT.Core.StatusType.Stunned, statusDuration: 1.5f);
			p.AddChild(s2);

			var mv = new PocahontasBreezeGlide();
			mv.Name = "MovementAbility";
			mv.Data = MakeAbilityData("Breeze Glide", 0, 5, Vector2.Zero);
			p.AddChild(mv);

			var ult = new PocahontasTidewaterTempest();
			ult.Name = "Ultimate";
			ult.Data = MakeAbilityData("Tidewater Tempest", 14, 0, new Vector2(5, -3));
			ult.Data.IsMultiHit = true;
			ult.Data.HitCount = 8;
			ult.AddChild(MakeHitbox("StormHitbox", 14, new Vector2(5, -3), new Vector2(90, 70), pi));
			p.AddChild(ult);
		}
	}
}
