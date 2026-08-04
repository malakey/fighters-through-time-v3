using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace FTT.FighterSim {

    [KlothoSerializable(300)]
    public partial class FighterInputCommand : CommandBase {
        public override bool IsContinuousInput => true;

        [KlothoOrder(0)] public int MoveX;
        [KlothoOrder(1)] public int MoveY;
        [KlothoOrder(2)] public int HeldButtons;
        [KlothoOrder(3)] public int PressedButtons;
        [KlothoOrder(4)] public int ReleasedButtons;

        public override void ClearOneShotForPrediction() {
            PressedButtons = 0;
            ReleasedButtons = 0;
        }
    }
}
