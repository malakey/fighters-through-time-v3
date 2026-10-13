using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Gameplay update 2026-10-12 (GU-E, user request 3): the encounter-lock
    /// readout. While a lock is Engaged the HUD shows "Enemies left: N"; for
    /// <see cref="FTT.Environment.EncounterLockRules.GoPromptSeconds"/> after a
    /// clear it shows a GO prompt — an arrow glyph plus translated text, never
    /// colour alone. Both are authored nodes under <c>SafeArea/EncounterLockPanel</c>
    /// with theme label roles (no font-size overrides), fed only by
    /// <see cref="EventBus.OnEncounterLockChanged"/>. The <c>HUD_CONTRACT</c>
    /// amendment is a closeout ledger item.
    /// </summary>
    public partial class StoryHUD {
        /// <summary>"Enemies left: {0}".</summary>
        public const string EncounterEnemiesLeftKey = "hud_encounter_enemies_left";

        private const string EncounterPanelPath = "SafeArea/EncounterLockPanel";

        private Control _encounterPanel;
        private Label _encounterEnemiesLeft;
        private Control _encounterGoPrompt;
        private Label _encounterGoArrow;
        private float _encounterGoTimer;
        private string _encounterLockID = "";

        /// <summary>True while the "Enemies left" counter shows. Test seam.</summary>
        public bool EncounterCounterVisible => _encounterEnemiesLeft != null && _encounterEnemiesLeft.Visible;

        /// <summary>The counter's current text. Test seam.</summary>
        public string EncounterCounterText => _encounterEnemiesLeft?.Text ?? "";

        /// <summary>True while the GO prompt shows. Test seam.</summary>
        public bool EncounterGoPromptVisible => _encounterGoPrompt != null && _encounterGoPrompt.Visible;

        /// <summary>The GO prompt's arrow glyph. Test seam.</summary>
        public string EncounterGoArrow => _encounterGoArrow?.Text ?? "";

        private void ResolveEncounterLockUI() {
            _encounterPanel = GetNodeOrNull<Control>(EncounterPanelPath);
            _encounterEnemiesLeft = GetNodeOrNull<Label>($"{EncounterPanelPath}/EnemiesLeft");
            _encounterGoPrompt = GetNodeOrNull<Control>($"{EncounterPanelPath}/GoPrompt");
            _encounterGoArrow = GetNodeOrNull<Label>($"{EncounterPanelPath}/GoPrompt/GoArrow");
            if (_encounterEnemiesLeft != null) _encounterEnemiesLeft.Visible = false;
            if (_encounterGoPrompt != null) _encounterGoPrompt.Visible = false;
        }

        private void OnEncounterLockChanged(EncounterLockPayload payload) {
            switch (payload.State) {
                case EncounterLockState.Engaged:
                    _encounterLockID = payload.LockID ?? "";
                    HideEncounterGoPrompt();
                    if (_encounterEnemiesLeft == null) return;
                    // A puzzle-only lock has nobody to count.
                    _encounterEnemiesLeft.Visible = payload.Total > 0;
                    _encounterEnemiesLeft.Text = string.Format(Tr(EncounterEnemiesLeftKey), payload.Remaining);
                    break;
                case EncounterLockState.Armed:
                    if (payload.LockID != _encounterLockID) return;
                    if (_encounterEnemiesLeft != null) _encounterEnemiesLeft.Visible = false;
                    break;
                case EncounterLockState.Cleared:
                    if (_encounterEnemiesLeft != null) _encounterEnemiesLeft.Visible = false;
                    ShowEncounterGoPrompt(payload.GoDirection);
                    break;
            }
        }

        private void ShowEncounterGoPrompt(EncounterGoDirection direction) {
            if (_encounterGoPrompt == null) return;
            if (_encounterGoArrow != null) _encounterGoArrow.Text = FTT.Environment.EncounterLockRules.GoGlyph(direction);
            _encounterGoPrompt.Visible = true;
            _encounterGoTimer = FTT.Environment.EncounterLockRules.GoPromptSeconds;
        }

        private void HideEncounterGoPrompt() {
            _encounterGoTimer = 0f;
            if (_encounterGoPrompt != null) _encounterGoPrompt.Visible = false;
        }

        private void TickEncounterGoPrompt(float delta) {
            if (_encounterGoTimer <= 0f) return;
            _encounterGoTimer -= delta;
            if (_encounterGoTimer <= 0f) HideEncounterGoPrompt();
        }
    }
}
