using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FTT.Core {

    public enum ContentTemplateKind {
        StoryLevel,
        FighterStage,
        PlayerPresentation,
        StandardEnemy,
        EliteEnemy,
        Boss,
        Projectile,
        PersistentConstruct,
        Pickup,
        Checkpoint,
        DialogueTrigger,
        PuzzleObject,
        Hazard,
        PooledVfx
    }

    [GlobalClass]
    public partial class ContentSceneContract : Resource {
        [Export] public int SchemaVersion = 1;
        [Export] public string ContractID = "";
        [Export] public ContentTemplateKind TemplateKind;
        [Export] public string[] RequiredNodePaths = Array.Empty<string>();
        [Export] public string[] RequiredRootGroups = Array.Empty<string>();
        [Export] public string AnimatedSpritePath = "";
        [Export] public string[] RequiredAnimationNames = Array.Empty<string>();
        [Export] public string[] RequiredEventCallbacks = Array.Empty<string>();
        [Export] public string[] RequiredCollisionLayerNames = Array.Empty<string>();
        [Export] public string[] RequiredInterfaceNames = Array.Empty<string>();
    }

    public static class ContentSceneContractValidator {
        public static IReadOnlyList<string> Validate(Node root, ContentSceneContract contract) {
            var errors = new List<string>();
            if (root == null) {
                errors.Add("Template root is null.");
                return errors;
            }
            if (contract == null) {
                errors.Add("Template contract is missing.");
                return errors;
            }
            if (contract.SchemaVersion != 1) errors.Add($"Unsupported contract schema {contract.SchemaVersion}.");
            if (string.IsNullOrWhiteSpace(contract.ContractID)) errors.Add("Contract ID is empty.");

            foreach (string path in contract.RequiredNodePaths ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(path) || root.GetNodeOrNull(new NodePath(path)) == null) {
                    errors.Add($"Missing required node path '{path}'.");
                }
            }
            foreach (string group in contract.RequiredRootGroups ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(group) || !root.IsInGroup(group)) errors.Add($"Root is missing required group '{group}'.");
            }
            foreach (string layerName in contract.RequiredCollisionLayerNames ?? Array.Empty<string>()) {
                if (!CollisionLayerExists(layerName)) errors.Add($"Project is missing collision layer '{layerName}'.");
            }
            foreach (string callback in contract.RequiredEventCallbacks ?? Array.Empty<string>()) {
                if (!TreeContainsMethod(root, callback)) errors.Add($"Template tree is missing event callback '{callback}'.");
            }
            foreach (string interfaceName in contract.RequiredInterfaceNames ?? Array.Empty<string>()) {
                if (!TreeImplementsInterface(root, interfaceName)) errors.Add($"Template tree is missing interface '{interfaceName}'.");
            }

            if ((contract.RequiredAnimationNames?.Length ?? 0) > 0) {
                AnimatedSprite2D sprite = string.IsNullOrWhiteSpace(contract.AnimatedSpritePath)
                    ? null
                    : root.GetNodeOrNull<AnimatedSprite2D>(new NodePath(contract.AnimatedSpritePath));
                if (sprite?.SpriteFrames == null) {
                    errors.Add($"AnimatedSprite2D '{contract.AnimatedSpritePath}' has no SpriteFrames resource.");
                } else {
                    foreach (string animationName in contract.RequiredAnimationNames) {
                        if (!sprite.SpriteFrames.HasAnimation(animationName)) errors.Add($"Missing animation '{animationName}'.");
                    }
                }
            }
            return errors;
        }

        private static bool CollisionLayerExists(string expectedName) {
            for (int layer = 1; layer <= 32; layer++) {
                string key = $"layer_names/2d_physics/layer_{layer}";
                if (ProjectSettings.GetSetting(key, "").AsString() == expectedName) return true;
            }
            return false;
        }

        private static bool TreeContainsMethod(Node node, string methodName) {
            if (HasMethod(node.GetType(), methodName)) return true;
            foreach (Node child in node.GetChildren()) {
                if (TreeContainsMethod(child, methodName)) return true;
            }
            return false;
        }

        private static bool HasMethod(Type type, string methodName) =>
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;

        private static bool TreeImplementsInterface(Node node, string interfaceName) {
            foreach (Type interfaceType in node.GetType().GetInterfaces()) {
                if (interfaceType.Name == interfaceName || interfaceType.FullName == interfaceName) return true;
            }
            foreach (Node child in node.GetChildren()) {
                if (TreeImplementsInterface(child, interfaceName)) return true;
            }
            return false;
        }
    }
}
