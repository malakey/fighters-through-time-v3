using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public partial class ContentTemplateMarker : Node {
        [Export] public string ContentID = "";
        [Export] public ContentSceneContract Contract;
        [Export] public bool UsesPlaceholderAssets = true;
        [Export] public ScenePoolConfig PoolConfig;

        public IReadOnlyList<string> ValidateTemplate() =>
            ContentSceneContractValidator.Validate(GetParent(), Contract);

        // Placeholder animation event targets. Production controllers implement the same names.
        public void ActivateHitbox() { }
        public void DeactivateHitbox() { }
        public void EmitFootstep() { }
        public void EmitPresentationEvent() { }
    }
}
