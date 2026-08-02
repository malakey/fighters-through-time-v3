using Godot;

namespace FTT.Characters {

    public partial class TrainingDummy : CharacterBody2D {
        [Export] public int MaxHP = 200;
        public int CurrentHP;

        private Label _hpLabel;
        private ColorRect _hpBar;
        private float _respawnTimer;
        private Vector2 _spawnPosition;
        private bool _dead;

        public override void _Ready() {
            CurrentHP = MaxHP;
            _spawnPosition = GlobalPosition;

            MotionMode = MotionModeEnum.Grounded;
            UpDirection = Vector2.Up;
            FloorStopOnSlope = true;
			CollisionLayer = 2;
			CollisionMask = 1 | 2;

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
            nameLabel.Text = "TRAINING DUMMY";
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
            hurtbox.CollisionLayer = 8;
            hurtbox.CollisionMask = 4;
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

        private void OnHit(float damage, Vector2 knockback, float hitstun, Vector2 hitPosition) {
            if (_dead) return;
            int dmg = (int)damage;
            CurrentHP -= dmg;
            SpawnDamageNumber(dmg, hitPosition);

            if (CurrentHP <= 0) {
                CurrentHP = 0;
                _dead = true;
                _respawnTimer = 2.5f;
            }
            UpdateHPDisplay();
        }

        private void SpawnDamageNumber(int damage, Vector2 position) {
            var num = new Label();
            num.Text = $"-{damage}";
            num.GlobalPosition = position + new Vector2(-15, -30);
            num.AddThemeColorOverride("font_color", damage >= 15 ? new Color(1, 0.3f, 0.1f) : new Color(1, 0.9f, 0.3f));
            num.AddThemeFontSizeOverride("font_size", damage >= 15 ? 22 : 16);
            GetParent()?.AddChild(num);

            var tween = num.CreateTween();
            tween.TweenProperty(num, "position:y", num.Position.Y - 50, 0.7f);
            tween.Parallel().TweenProperty(num, "modulate:a", 0f, 0.7f);
            tween.TweenCallback(Callable.From(() => num.QueueFree()));
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
            if (!IsOnFloor()) {
                var vel = Velocity;
                vel.Y += 1800f * (float)delta;
                Velocity = vel;
                MoveAndSlide();
            }
        }
    }
}
