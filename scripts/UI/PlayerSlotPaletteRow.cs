using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W5 (G12). The one Settings → Gameplay row for the player-slot
    /// palette (Default, Blue/Orange, High-Contrast). Self-wiring on purpose: the
    /// row is authored into <c>Settings.tscn</c> with this script, so the rest of
    /// <c>SettingsMenu</c> (owned by W6) is untouched — it only has to be in the
    /// tab for <c>FocusChainBuilder</c> to pick up its selector.
    ///
    /// <para>The choice persists in <see cref="GlobalSaveData.PlayerSlotPalette"/>
    /// and is presentation-only: it recolors the ownership outline and HUD slot
    /// colours locally and never enters the simulation, a snapshot or a hash.</para>
    /// </summary>
    public partial class PlayerSlotPaletteRow : HBoxContainer {

        private OptionButton _select;

        /// <summary>The selector. Test seam.</summary>
        internal OptionButton Selector => _select;

        public override void _Ready() {
            _select = GetNodeOrNull<OptionButton>("PaletteSelect");
            if (_select == null) return;
            _select.Clear();
            for (int index = 0; index < PlayerSlotPalettes.Count; index++) {
                _select.AddItem(Tr(PlayerSlotPalettes.LabelKey((PlayerSlotPalette)index)), index);
            }
            _select.Select((int)PlayerSlotPalettes.Active);
            _select.ItemSelected += OnSelected;
        }

        /// <summary>Stores the chosen palette. Public so a test can drive it without input.</summary>
        public void Apply(int paletteIndex) {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data == null) return;
            data.PlayerSlotPalette = (int)PlayerSlotPalettes.Sanitize(paletteIndex);
            SaveManager.Instance.SaveGlobalData();
        }

        private void OnSelected(long index) => Apply((int)index);
    }
}
