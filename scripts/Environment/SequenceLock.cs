using Godot;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Ordered-glyph puzzle (Level 8 hieroglyph gate). Child
    /// <see cref="SequenceGlyph"/> nodes must be activated in ascending
    /// <see cref="SequenceGlyph.OrderIndex"/> order; a wrong pick unlights every
    /// glyph and restarts the sequence. Completion sets
    /// <see cref="ConditionID"/> on the <see cref="PuzzleManagerPath"/> manager.
    /// </summary>
    public partial class SequenceLock : Node2D, IStoryRewindable {
        [Signal] public delegate void SequenceAdvancedEventHandler(int progress);
        [Signal] public delegate void SequenceFailedEventHandler(string glyphID);
        [Signal] public delegate void SequenceResetEventHandler();
        [Signal] public delegate void SequenceCompletedEventHandler(string lockID);

        [Export] public string LockID = "";
        [Export] public NodePath PuzzleManagerPath;
        [Export] public string ConditionID = "sequence_complete";
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly List<SequenceGlyph> _glyphs = new();
        private int _checkpointProgress;
        private bool _checkpointCompleted;

        public int Progress { get; private set; }
        public bool IsCompleted { get; private set; }
        public IReadOnlyList<SequenceGlyph> Glyphs => _glyphs;
        public int GlyphCount => _glyphs.Count;
        public SequenceGlyph ExpectedGlyph => Progress >= 0 && Progress < _glyphs.Count ? _glyphs[Progress] : null;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            CollectGlyphs();
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        /// <summary>Rebuilds the ordered glyph list from the current children.</summary>
        public void CollectGlyphs() {
            _glyphs.Clear();
            foreach (Node child in GetChildren()) {
                if (child is SequenceGlyph glyph) _glyphs.Add(glyph);
            }
            _glyphs.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
        }

        /// <summary>Returns true when the glyph was the next correct one.</summary>
        public bool Activate(SequenceGlyph glyph) {
            if (glyph == null || IsCompleted) return false;
            if (_glyphs.Count == 0) CollectGlyphs();
            if (!ReferenceEquals(glyph, ExpectedGlyph)) {
                EmitSignal(SignalName.SequenceFailed, glyph.GlyphID);
                ResetSequence();
                return false;
            }
            glyph.SetLit(true);
            Progress++;
            EmitSignal(SignalName.SequenceAdvanced, Progress);
            if (Progress >= _glyphs.Count) Complete();
            return true;
        }

        public void ResetSequence() {
            Progress = 0;
            foreach (SequenceGlyph glyph in _glyphs) glyph.SetLit(false);
            EmitSignal(SignalName.SequenceReset);
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointProgress = Progress;
            _checkpointCompleted = IsCompleted;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool reset = RewindPolicy == StoryRewindPolicy.ResetToInitialState;
            IsCompleted = !reset && _checkpointCompleted;
            Progress = reset ? 0 : _checkpointProgress;
            for (int index = 0; index < _glyphs.Count; index++) _glyphs[index].SetLit(IsCompleted || index < Progress);
            if (IsCompleted) GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, true);
        }

        private void Complete() {
            IsCompleted = true;
            GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, true);
            EmitSignal(SignalName.SequenceCompleted, LockID);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
