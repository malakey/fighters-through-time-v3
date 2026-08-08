using Godot;
using FTT.Characters;

namespace FTT.Environment {

    /// <summary>
    /// One interactable glyph of a <see cref="SequenceLock"/>. Add these as children
    /// of the lock; the lock orders them by <see cref="OrderIndex"/>.
    /// </summary>
    public partial class SequenceGlyph : Node2D, IInteractable {
        [Signal] public delegate void GlyphActivatedEventHandler(string glyphID);

        [Export] public string GlyphID = "";
        /// <summary>Position in the correct activation order, lowest first.</summary>
        [Export(PropertyHint.Range, "0,32,1")] public int OrderIndex;
        [Export] public string InteractionPromptKey = "interaction_press_glyph";
        [Export] public NodePath SequenceLockPath = "..";
        [Export] public NodePath VisualPath = "Visual";
        [Export] public Color LitColor = new(1f, 0.85f, 0.35f, 1f);
        [Export] public Color UnlitColor = new(0.34f, 0.29f, 0.22f, 1f);

        public bool IsLit { get; private set; }
        public string InteractionID => GlyphID;
        public string PromptKey => InteractionPromptKey;
        public SequenceLock Lock => GetNodeOrNull<SequenceLock>(SequenceLockPath);

        public override void _Ready() {
            AddToGroup("puzzle_object");
            ApplyPresentation();
        }

        public bool CanInteract(PlayerController player) =>
            player != null && !IsLit && Lock?.IsCompleted != true;

        public void Interact(PlayerController player) {
            SequenceLock sequenceLock = Lock;
            if (sequenceLock == null) return;
            sequenceLock.Activate(this);
            EmitSignal(SignalName.GlyphActivated, GlyphID);
        }

        public void SetLit(bool lit) {
            IsLit = lit;
            ApplyPresentation();
        }

        private void ApplyPresentation() {
            if (GetNodeOrNull<CanvasItem>(VisualPath) is CanvasItem visual) {
                visual.Modulate = IsLit ? LitColor : UnlitColor;
            }
        }
    }
}
