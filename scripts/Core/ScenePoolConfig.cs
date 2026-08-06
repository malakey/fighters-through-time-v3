using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Core {

    [GlobalClass]
    public partial class ScenePoolConfig : Resource {
        [Export] public string ConfigID = "";
        [Export(PropertyHint.Range, "1,1000,1")] public int MaxWarmUpInstances = 250;
        [Export] public PoolDefinition[] PoolDefinitions = Array.Empty<PoolDefinition>();

        public int GetWarmUpInstanceCount() {
            int total = 0;
            foreach (PoolDefinition definition in PoolDefinitions ?? Array.Empty<PoolDefinition>()) {
                if (definition != null) total += Math.Max(0, definition.WarmUpCount);
            }
            return total;
        }

        public int GetMaxCapacityCount() {
            int total = 0;
            foreach (PoolDefinition definition in PoolDefinitions ?? Array.Empty<PoolDefinition>()) {
                if (definition != null) total += Math.Max(0, definition.MaxCapacity);
            }
            return total;
        }

        public IReadOnlyList<string> ValidateBudget() {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(ConfigID)) errors.Add("Pool config ID is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoolDefinition definition in PoolDefinitions ?? Array.Empty<PoolDefinition>()) {
                if (definition == null) {
                    errors.Add("Pool definition is null.");
                    continue;
                }
                if (!definition.HasValidBudget()) errors.Add($"Pool '{definition.PoolID}' has an invalid template or capacity budget.");
                if (!ids.Add(definition.PoolID)) errors.Add($"Pool ID '{definition.PoolID}' is duplicated.");
            }
            if (GetWarmUpInstanceCount() > MaxWarmUpInstances) {
                errors.Add($"Warm-up total {GetWarmUpInstanceCount()} exceeds scene budget {MaxWarmUpInstances}.");
            }
            return errors;
        }
    }
}
