using Godot;
using System.Collections.Generic;
using FTT.Characters;

namespace FTT.Environment {

    /// <summary>
    /// Story-only registry for environment zones that scale a
    /// <see cref="PlayerController"/>'s movement speed or gravity while the player
    /// is inside them.
    ///
    /// Zones must never write <c>PlayerController.EnvironmentMoveMultiplier</c> or
    /// <c>EnvironmentGravityScale</c> directly: two overlapping zones would each
    /// restore 1.0 on exit and silently cancel the other one. Every source
    /// registers itself here instead and the registry publishes the product of all
    /// live sources, so exits restore exactly the remaining stack.
    ///
    /// This is deliberately separate from <see cref="FTT.Combat.StatusController"/>:
    /// the status system enforces newest-status-replacement with a single active
    /// status, and terrain slow must not evict (or be evicted by) a real
    /// TimeDilation/Root application. Fighter Mode never uses this — the
    /// deterministic simulation in <c>scripts/FighterSim/</c> is authoritative
    /// there and does not read either hook.
    /// </summary>
    public static class EnvironmentPlayerModifiers {
        private static readonly Dictionary<PlayerController, Dictionary<ulong, float>> MoveSources = new();
        private static readonly Dictionary<PlayerController, Dictionary<ulong, float>> GravitySources = new();

        public static void SetMoveMultiplier(PlayerController player, GodotObject source, float multiplier) =>
            Set(MoveSources, player, source, multiplier, isGravity: false);

        public static void ClearMoveMultiplier(PlayerController player, GodotObject source) =>
            Clear(MoveSources, player, source, isGravity: false);

        public static void SetGravityScale(PlayerController player, GodotObject source, float scale) =>
            Set(GravitySources, player, source, scale, isGravity: true);

        public static void ClearGravityScale(PlayerController player, GodotObject source) =>
            Clear(GravitySources, player, source, isGravity: true);

        /// <summary>Drops every registration owned by one zone (call from <c>_ExitTree</c>).</summary>
        public static void ClearSource(GodotObject source) {
            if (source == null) return;
            ulong sourceID = source.GetInstanceId();
            ClearSourceFrom(MoveSources, sourceID, isGravity: false);
            ClearSourceFrom(GravitySources, sourceID, isGravity: true);
        }

        public static int MoveSourceCount(PlayerController player) => SourceCount(MoveSources, player);
        public static int GravitySourceCount(PlayerController player) => SourceCount(GravitySources, player);

        private static void Set(
            Dictionary<PlayerController, Dictionary<ulong, float>> table,
            PlayerController player,
            GodotObject source,
            float value,
            bool isGravity) {
            if (player == null || source == null || !GodotObject.IsInstanceValid(player)) return;
            Prune(table);
            if (!table.TryGetValue(player, out Dictionary<ulong, float> sources)) {
                sources = new Dictionary<ulong, float>();
                table[player] = sources;
            }
            sources[source.GetInstanceId()] = value;
            Publish(table, player, isGravity);
        }

        private static void Clear(
            Dictionary<PlayerController, Dictionary<ulong, float>> table,
            PlayerController player,
            GodotObject source,
            bool isGravity) {
            if (player == null || source == null) return;
            if (!table.TryGetValue(player, out Dictionary<ulong, float> sources)) return;
            if (!sources.Remove(source.GetInstanceId())) return;
            if (sources.Count == 0) table.Remove(player);
            Publish(table, player, isGravity);
        }

        private static void ClearSourceFrom(
            Dictionary<PlayerController, Dictionary<ulong, float>> table,
            ulong sourceID,
            bool isGravity) {
            var touched = new List<PlayerController>();
            foreach ((PlayerController player, Dictionary<ulong, float> sources) in table) {
                if (sources.Remove(sourceID)) touched.Add(player);
            }
            foreach (PlayerController player in touched) {
                if (table.TryGetValue(player, out Dictionary<ulong, float> sources) && sources.Count == 0) {
                    table.Remove(player);
                }
                Publish(table, player, isGravity);
            }
        }

        private static int SourceCount(
            Dictionary<PlayerController, Dictionary<ulong, float>> table,
            PlayerController player) =>
            player != null && table.TryGetValue(player, out Dictionary<ulong, float> sources) ? sources.Count : 0;

        private static void Publish(
            Dictionary<PlayerController, Dictionary<ulong, float>> table,
            PlayerController player,
            bool isGravity) {
            if (!GodotObject.IsInstanceValid(player)) {
                table.Remove(player);
                return;
            }
            float product = 1f;
            if (table.TryGetValue(player, out Dictionary<ulong, float> sources)) {
                foreach (float value in sources.Values) product *= value;
            }
            if (isGravity) player.EnvironmentGravityScale = product;
            else player.EnvironmentMoveMultiplier = product;
        }

        private static void Prune(Dictionary<PlayerController, Dictionary<ulong, float>> table) {
            List<PlayerController> stale = null;
            foreach (PlayerController player in table.Keys) {
                if (!GodotObject.IsInstanceValid(player)) (stale ??= new List<PlayerController>()).Add(player);
            }
            if (stale == null) return;
            foreach (PlayerController player in stale) table.Remove(player);
        }
    }
}
