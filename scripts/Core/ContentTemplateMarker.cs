using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public partial class ContentTemplateMarker : Node {
        [Export] public string ContentID = "";

        // Scene-assigned scripted data: pinned on first sight
        // (AuthoredResources.Pin), so a level teardown can never leave the
        // contract's wrapper collectable while the cached resource is reloaded.
        [Export] public ContentSceneContract Contract {
            get => _contract;
            set => _contract = AuthoredResources.Pin(value);
        }

        [Export] public bool UsesPlaceholderAssets = true;

        [Export] public ScenePoolConfig PoolConfig {
            get => _poolConfig;
            set => _poolConfig = AuthoredResources.Pin(value);
        }

        private ContentSceneContract _contract;
        private ScenePoolConfig _poolConfig;

        public IReadOnlyList<string> ValidateTemplate() =>
            ContentSceneContractValidator.Validate(GetParent(), Contract);

        // Placeholder animation event targets. Production controllers implement the same names.
        public void ActivateHitbox() { }
        public void DeactivateHitbox() { }
        public void EmitFootstep() { }
        public void EmitPresentationEvent() { }
    }
}
