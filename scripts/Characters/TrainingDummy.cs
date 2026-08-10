using Godot;

namespace FTT.Characters {

    public partial class TrainingDummy : CharacterBody2D {
        [Export] public int MaxHP = 200;
        public int CurrentHP;

        /// <summary>Raised with applied damage each time a hit lands; used by tutorial calibration.</summary>
        public event System.Action<int> HitLanded;

        private Label _hpLabel;
        private ColorRect _hpBar;
        private float _respawnTimer;
        private Vector2 _spawnPosition;
        private bool _dead;

        // === Scripted telegraphed attack (tutorial block calibration, audit M-3) ===
        // The dummy is otherwise passive; the tutorial's block step arms this
        // slow, clearly telegraphed swipe so the player can practice holding
        // Block and watch shield charges fall. Deterministic frame counters at
        // 60 Hz, mirroring the enemy telegraph/active/recovery convention.

        /// <summary>Frames the dummy rests between scripted swipes.</summary>
        public const int ScriptedAttackIdleFrames = 90;
        /// <summary>Telegraph length before a scripted swipe lands — generous, it is a lesson.</summary>
        public const int ScriptedAttackTelegraphFrames = 45;
        /// <summary>Damage of the scripted swipe when it is not blocked.</summary>
        public const float ScriptedAttackDamage = 5f;
        /// <summary>Range within which the scripted swipe reaches the target.</summary>
        public const float ScriptedAttackRange = 420f;

        /// <summary>True while the scripted attack cycle is running.</summary>
        public bool ScriptedAttacksActive { get; private set; }

        /// <summary>Raised after each scripted swipe resolves, with the HP damage applied (0 when blocked).</summary>
        public event System.Action<float> ScriptedStrikeResolved;

        private FTT.Characters.PlayerController _scriptedTarget;
        private int _scriptedAttackFrame;
        private bool _scriptedTelegraphing;

        public override void _Ready() {
            CurrentHP = MaxHP;
            _spawnPosition = GlobalPosition;

            MotionMode = MotionModeEnum.Grounded;
            UpDirection = Vector2.Up;
            FloorStopOnSlope = true;
			CollisionLayer = FTT.Core.CollisionLayers.Enemy;
			CollisionMask = FTT.Core.CollisionLayers.EnemyBodyMask;

            var col = new CollisionShape2D();
            var colRect = new RectangleShape2D();
            colRect.Size = new Vector2(50, 80);
            col.Shape = colRect;
            col.Position = new Vector2(0, -40);
            AddChild(col);

            var body = new ColorRect();
            body.Size = new Vector2(50, 80);
            body.Position = new Vector2(-25, -80);
            body.Color = new Color(0.7f, 0.2f, 0.2f);
            AddChild(body);

            var head = new ColorRect();
            head.Size = new Vector2(30, 20);
            head.Position = new Vector2(-15, -100);
            head.Color = new Color(0.8f, 0.6f, 0.4f);
            AddChild(head);

            var arms = new ColorRect();
            arms.Size = new Vector2(60, 6);
            arms.Position = new Vector2(-30, -65);
            arms.Color = new Color(0.6f, 0.15f, 0.15f);
            AddChild(arms);

            var nameLabel = new Label();
            nameLabel.Text = Tr("tutorial_dummy_name");
            nameLabel.Position = new Vector2(-55, -125);
            nameLabel.CustomMinimumSize = new Vector2(110, 20);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(nameLabel);

            var hpBg = new ColorRect();
            hpBg.Size = new Vector2(60, 8);
            hpBg.Position = new Vector2(-30, -135);
            hpBg.Color = new Color(0.15f, 0.15f, 0.15f);
            AddChild(hpBg);

            _hpBar = new ColorRect();
            _hpBar.Size = new Vector2(60, 8);
            _hpBar.Position = new Vector2(-30, -135);
            _hpBar.Color = new Color(0.2f, 0.8f, 0.2f);
            AddChild(_hpBar);

            _hpLabel = new Label();
            _hpLabel.Position = new Vector2(-30, -150);
            _hpLabel.CustomMinimumSize = new Vector2(60, 15);
            _hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _hpLabel.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_hpLabel);
            UpdateHPDisplay();

            var hurtbox = new FTT.Combat.Hurtbox();
            hurtbox.Name = "Hurtbox";
            hurtbox.OwnerPlayerIndex = 99;
            hurtbox.CollisionLayer = FTT.Core.CollisionLayers.EnemyHurtbox;
            hurtbox.CollisionMask = FTT.Core.CollisionLayers.PlayerHitbox | FTT.Core.CollisionLayers.Projectile;
            hurtbox.Monitorable = true;
            hurtbox.Monitoring = true;
            var hbShape = new CollisionShape2D();
            var hbRect = new RectangleShape2D();
            hbRect.Size = new Vector2(50, 80);
            hbShape.Shape = hbRect;
            hbShape.Position = new Vector2(0, -40);
            hurtbox.AddChild(hbShape);
            AddChild(hurtbox);
            hurtbox.OnHit += OnHit;
        }

        private float OnHit(FTT.Combat.HitPayload hit) {
            if (_dead) return 0f;
            int previousHP = CurrentHP;
            int dmg = Mathf.Max(0, (int)Mathf.Round(hit.Damage));
            CurrentHP = Mathf.Max(0, CurrentHP - dmg);
            int damageApplied = previousHP - CurrentHP;
            SpawnDamageNumber(damageApplied, hit.HitOrigin);

            if (CurrentHP <= 0) {
                CurrentHP = 0;
                _dead = true;
                _respawnTimer = 2.5f;
            }
            UpdateHPDisplay();
            if (damageApplied > 0) HitLanded?.Invoke(damageApplied);
            return damageApplied;
        }

        private void SpawnDamageNumber(int damage, Vector2 position) {
            FTT.UI.FloatingDamageNumber.Show(damage, position + new Vector2(-15, -30), GetParent());
        }

        private void UpdateHPDisplay() {
            if (_hpLabel != null) _hpLabel.Text = $"{CurrentHP}/{MaxHP}";
            if (_hpBar != null) {
                float ratio = Mathf.Max(0, (float)CurrentHP / MaxHP);
                _hpBar.Size = new Vector2(60f * ratio, 8);
                _hpBar.Color = ratio > 0.5f ? new Color(0.2f, 0.8f, 0.2f)
                    : ratio > 0.25f ? new Color(0.8f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (_dead) {
                Modulate = new Color(1, 1, 1, 0.3f);
                _respawnTimer -= (float)delta;
                if (_respawnTimer <= 0) {
                    _dead = false;
                    CurrentHP = MaxHP;
                    GlobalPosition = _spawnPosition;
                    UpdateHPDisplay();
                }
                return;
            }

            Modulate = Colors.White;
            ProcessScriptedAttack();
            if (!IsOnFloor()) {
                var vel = Velocity;
                vel.Y += 1800f * (float)delta;
                Velocity = vel;
                MoveAndSlide();
            }
        }

        /// <summary>Arms the scripted telegraphed swipe cycle against one target.</summary>
        public void BeginScriptedAttacks(FTT.Characters.PlayerController target) {
            _scriptedTarget = target;
            _scriptedAttackFrame = 0;
            _scriptedTelegraphing = false;
            ScriptedAttacksActive = target != null;
        }

        /// <summary>Stops the scripted swipe cycle and clears the telegraph tint.</summary>
        public void EndScriptedAttacks() {
            ScriptedAttacksActive = false;
            _scriptedTarget = null;
            _scriptedTelegraphing = false;
            Modulate = Colors.White;
        }

        private void ProcessScriptedAttack() {
            if (!ScriptedAttacksActive) return;
            if (_scriptedTarget == null || !IsInstanceValid(_scriptedTarget)) {
                EndScriptedAttacks();
                return;
            }

            _scriptedAttackFrame++;
            if (!_scriptedTelegraphing) {
                if (_scriptedAttackFrame >= ScriptedAttackIdleFrames) {
                    _scriptedTelegraphing = true;
                    _scriptedAttackFrame = 0;
                }
                return;
            }

            // Telegraph: pulse toward an amber warning tint so the wind-up reads.
            float progress = Mathf.Clamp((float)_scriptedAttackFrame / ScriptedAttackTelegraphFrames, 0f, 1f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(progress * Mathf.Pi * 4f);
            Modulate = Colors.White.Lerp(new Color(1f, 0.7f, 0.2f), 0.6f * pulse + 0.2f);

            if (_scriptedAttackFrame >= ScriptedAttackTelegraphFrames) {
                Modulate = Colors.White;
                _scriptedTelegraphing = false;
                _scriptedAttackFrame = 0;
                DeliverScriptedStrike();
            }
        }

        /// <summary>
        /// Resolves one scripted swipe against the armed target through the real
        /// hurtbox/block path — a held block absorbs it (raising the block-absorb
        /// event the tutorial counts), an open stance takes the small hit. Public
        /// so tests can exercise the strike without stepping the telegraph cycle.
        /// Returns the HP damage applied (0 when blocked or out of range).
        /// </summary>
        public float DeliverScriptedStrike() {
            if (_scriptedTarget == null || !IsInstanceValid(_scriptedTarget)) return 0f;
            if (GlobalPosition.DistanceTo(_scriptedTarget.GlobalPosition) > ScriptedAttackRange) return 0f;
            var hurtbox = _scriptedTarget.GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
            if (hurtbox == null) return 0f;

            float applied = hurtbox.TakeHit(new FTT.Combat.HitPayload {
                AttackerIndex = 99,
                TargetIndex = _scriptedTarget.PlayerIndex,
                AttackID = "tutorial_dummy_swipe",
                HitboxID = "primary",
                AttackClass = FTT.Combat.AttackClass.Basic,
                Damage = ScriptedAttackDamage,
                Knockback = new Vector2(1.5f, -0.5f),
                HitstunDuration = 0.1f,
                HitOrigin = GlobalPosition,
                AttackerFacingRight = _scriptedTarget.GlobalPosition.X >= GlobalPosition.X,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusIntensity = 1f,
                ScreenShakeIntensity = 0.1f,
                ScreenShakeDuration = 0.05f
            });
            ScriptedStrikeResolved?.Invoke(applied);
            return applied;
        }
    }
}
