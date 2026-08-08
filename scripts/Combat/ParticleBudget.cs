using System.Collections.Generic;

namespace FTT.Combat {

    /// <summary>
    /// Overflow policy for the shared particle budget.
    /// </summary>
    public enum ParticleBudgetPolicy {
        /// <summary>Refuse the new reservation when it does not fit.</summary>
        Refuse,
        /// <summary>Evict the oldest reservations until the new one fits.</summary>
        StealOldest
    }

    /// <summary>
    /// Bounds the number of simultaneously emitting particles against the project's
    /// 500-particle budget (AGENTS.md performance targets; the baseline runner has
    /// measured it since Package 0 but nothing enforced it).
    ///
    /// Pure C#: no Godot nodes, so the accounting rules are testable without the
    /// engine. Emitters key their reservation on their instance id.
    /// </summary>
    public sealed class ParticleBudgetRegistry {
        /// <summary>Project-wide simultaneous particle target.</summary>
        public const int DefaultMaxParticles = 500;

        private readonly Dictionary<ulong, int> _costs = new();
        private readonly List<ulong> _order = new();

        public ParticleBudgetRegistry(int maxParticles = DefaultMaxParticles,
            ParticleBudgetPolicy policy = ParticleBudgetPolicy.StealOldest) {
            MaxParticles = maxParticles < 1 ? 1 : maxParticles;
            Policy = policy;
        }

        public int MaxParticles { get; }
        public ParticleBudgetPolicy Policy { get; }
        public int ActiveParticles { get; private set; }
        public int ActiveEmitters => _order.Count;

        /// <summary>
        /// Reserves <paramref name="cost"/> particles for <paramref name="id"/>.
        /// Re-reserving an id replaces its previous cost. Returns false when the
        /// request cannot be satisfied; <paramref name="evicted"/> lists reservations
        /// dropped to make room (always empty under <see cref="ParticleBudgetPolicy.Refuse"/>).
        /// </summary>
        public bool TryReserve(ulong id, int cost, out IReadOnlyList<ulong> evicted) {
            var dropped = new List<ulong>();
            evicted = dropped;
            if (cost <= 0) return false;
            // A single emitter larger than the whole budget is always refused; no
            // amount of eviction could make it fit.
            if (cost > MaxParticles) return false;

            Release(id);

            if (ActiveParticles + cost > MaxParticles) {
                if (Policy == ParticleBudgetPolicy.Refuse) return false;
                int index = 0;
                while (ActiveParticles + cost > MaxParticles && index < _order.Count) {
                    ulong oldest = _order[index];
                    dropped.Add(oldest);
                    ActiveParticles -= _costs[oldest];
                    _costs.Remove(oldest);
                    _order.RemoveAt(index);
                }
                if (ActiveParticles + cost > MaxParticles) return false;
            }

            _costs[id] = cost;
            _order.Add(id);
            ActiveParticles += cost;
            return true;
        }

        public bool TryReserve(ulong id, int cost) => TryReserve(id, cost, out _);

        public bool Release(ulong id) {
            if (!_costs.TryGetValue(id, out int cost)) return false;
            _costs.Remove(id);
            _order.Remove(id);
            ActiveParticles -= cost;
            if (ActiveParticles < 0) ActiveParticles = 0;
            return true;
        }

        public bool IsReserved(ulong id) => _costs.ContainsKey(id);

        public void Clear() {
            _costs.Clear();
            _order.Clear();
            ActiveParticles = 0;
        }
    }

    /// <summary>Process-wide particle budget shared by every pooled emitter.</summary>
    public static class ParticleBudget {
        public static ParticleBudgetRegistry Shared { get; } = new();
    }
}
