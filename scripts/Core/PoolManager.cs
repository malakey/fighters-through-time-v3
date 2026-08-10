using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Core {

    public interface IPoolable {
        void OnSpawn();
        void OnDespawn();
    }

    public enum PoolOverflowPolicy {
        Grow,
        RecycleOldest,
        Reject
    }

    public readonly record struct PoolStats(int Active, int Inactive, int MaxCapacity, PoolOverflowPolicy OverflowPolicy);

    public partial class PooledNode : Node2D {
        public PackedScene SceneOrigin { get; internal set; }
        public string PoolID { get; internal set; } = "";

        public void ReturnToPool() {
            if (PoolManager.Instance != null) PoolManager.Instance.Release(this);
            else QueueFree();
        }
    }

    public partial class PoolManager : Node {
        public static PoolManager Instance { get; private set; }

        private class Pool {
            public string PoolID;
            public string TemplateKey;
            public PackedScene Template;
            public Queue<Node> Inactive = new();
            public List<Node> Active = new();
            public int MaxCapacity;
            public PoolOverflowPolicy OverflowPolicy;
            public Node InactiveContainer;
        }

        private readonly Dictionary<string, Pool> _pools = new();
        private readonly Dictionary<string, Pool> _poolsByID = new(StringComparer.Ordinal);
        private Node _poolRoot;

        public override void _Ready() {
            Instance = this;
            _poolRoot = new Node();
            _poolRoot.Name = "PoolRoot";
            AddChild(_poolRoot);
        }

        public override void _ExitTree() {
            if (Instance == this) Instance = null;
        }

        public void RegisterPool(PackedScene template, int warmUpCount, int maxCapacity, PoolOverflowPolicy overflowPolicy) {
            if (template == null) throw new ArgumentNullException(nameof(template));
            RegisterPool(GetTemplateKey(template), template, warmUpCount, maxCapacity, overflowPolicy);
        }

        public void RegisterPool(string poolID, PackedScene template, int warmUpCount, int maxCapacity, PoolOverflowPolicy overflowPolicy) {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(poolID)) throw new ArgumentException("Pool ID cannot be empty.", nameof(poolID));
            poolID = poolID.Trim();
            string key = GetTemplateKey(template);
            if (_poolsByID.TryGetValue(poolID, out Pool existingByID)) {
                if (!string.Equals(existingByID.TemplateKey, key, StringComparison.Ordinal)) {
                    throw new ArgumentException($"Pool ID '{poolID}' is already registered to a different template.", nameof(poolID));
                }
                return;
            }
            if (_pools.TryGetValue(key, out Pool existingByTemplate)) {
                _poolsByID[poolID] = existingByTemplate;
                return;
            }

            int capacity = Math.Max(1, maxCapacity);

            var pool = new Pool {
                PoolID = poolID,
                TemplateKey = key,
                Template = template,
                MaxCapacity = capacity,
                OverflowPolicy = overflowPolicy
            };

            pool.InactiveContainer = new Node();
            pool.InactiveContainer.Name = $"Pool_{System.IO.Path.GetFileNameWithoutExtension(key)}";
            _poolRoot.AddChild(pool.InactiveContainer);

            int initialCount = Math.Clamp(warmUpCount, 0, capacity);
            for (int i = 0; i < initialCount; i++) {
                var node = CreateInstance(template);
                node.ProcessMode = ProcessModeEnum.Disabled;
                pool.InactiveContainer.AddChild(node);
                if (node is Node2D node2D) node2D.Visible = false;
                pool.Inactive.Enqueue(node);
            }

            _pools[key] = pool;
            _poolsByID[poolID] = pool;
        }

        public void WarmFromConfig(ScenePoolConfig config) {
            if (config?.PoolDefinitions == null) return;
            IReadOnlyList<string> errors = config.ValidateBudget();
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(config));
            foreach (var def in config.PoolDefinitions) {
                if (def?.SceneTemplate == null) continue;
                RegisterPool(def.PoolID, def.SceneTemplate, def.WarmUpCount, def.MaxCapacity, def.OverflowPolicy);
            }
        }

        public Node Spawn(PackedScene template, Vector2 position, Node parent = null) {
            if (template == null) throw new ArgumentNullException(nameof(template));
            string key = GetTemplateKey(template);
            if (!_pools.TryGetValue(key, out var pool)) {
                RegisterPool(template, 1, 50, PoolOverflowPolicy.Grow);
                pool = _pools[key];
            }

            return SpawnFromPool(pool, position, parent);
        }

        public Node Spawn(string poolID, Vector2 position, Node parent = null) {
            if (string.IsNullOrWhiteSpace(poolID) || !_poolsByID.TryGetValue(poolID.Trim(), out Pool pool)) return null;
            return SpawnFromPool(pool, position, parent);
        }

        private Node SpawnFromPool(Pool pool, Vector2 position, Node parent) {
            PackedScene template = pool.Template;

            Node node;
            if (pool.Inactive.Count > 0) {
                node = pool.Inactive.Dequeue();
                pool.InactiveContainer.RemoveChild(node);
            } else {
                // The pool looks exhausted. Stale entries - nodes freed behind the
                // pool's back - would otherwise count against capacity forever, so
                // reconcile before deciding. Off the hot path: only reached once the
                // inactive queue has run dry.
                PurgeInvalidActive();
                int totalCount = pool.Active.Count;
                switch (pool.OverflowPolicy) {
                    case PoolOverflowPolicy.Reject:
                        if (totalCount >= pool.MaxCapacity) return null;
                        node = CreateInstance(template);
                        break;
                    case PoolOverflowPolicy.RecycleOldest:
                        if (totalCount >= pool.MaxCapacity && pool.Active.Count > 0) {
                            var oldest = pool.Active[0];
                            Release(oldest);
                            // Release only enqueues when the node really belonged to
                            // this pool; fall back to a fresh instance if it did not.
                            if (pool.Inactive.Count > 0) {
                                node = pool.Inactive.Dequeue();
                                pool.InactiveContainer.RemoveChild(node);
                            } else {
                                node = CreateInstance(template);
                            }
                        } else {
                            node = CreateInstance(template);
                        }
                        break;
                    default: // Grow
                        // M-25: Grow really grows. MaxCapacity is the warm-up /
                        // budget hint, not a hard cap — the old arm was identical
                        // to Reject, so projectiles, story mobs, and damage
                        // numbers silently stopped spawning during dense fights.
                        node = CreateInstance(template);
                        break;
                }
            }

            var targetParent = parent ?? GetTree().CurrentScene;
            if (targetParent == null) throw new InvalidOperationException("A pool spawn requires a parent or current scene.");
            targetParent.AddChild(node);

            if (node is Node2D node2d) {
                node2d.GlobalPosition = position;
                node2d.Visible = true;
            }
            node.ProcessMode = ProcessModeEnum.Inherit;

            if (node is IPoolable poolable) poolable.OnSpawn();
            if (node is PooledNode pooledNode) {
                pooledNode.SceneOrigin = template;
                pooledNode.PoolID = pool.PoolID;
            }

            pool.Active.Add(node);
            return node;
        }

        public void Release(Node node) {
            if (node == null || !IsInstanceValid(node)) return;

            string key = null;
            if (node is PooledNode pooledNode &&
                !string.IsNullOrWhiteSpace(pooledNode.PoolID) &&
                _poolsByID.TryGetValue(pooledNode.PoolID, out Pool identifiedPool)) {
                key = identifiedPool.TemplateKey;
            } else if (node is PooledNode originNode && originNode.SceneOrigin != null) {
                key = GetTemplateKey(originNode.SceneOrigin);
            } else {
                foreach (var kvp in _pools) {
                    if (kvp.Value.Active.Contains(node)) {
                        key = kvp.Key;
                        break;
                    }
                }
            }

            if (key == null || !_pools.TryGetValue(key, out var pool)) {
                node.QueueFree();
                return;
            }
            if (!pool.Active.Contains(node)) return;

            if (node is IPoolable poolable) poolable.OnDespawn();

            pool.Active.Remove(node);
            node.GetParent()?.RemoveChild(node);

            if (node is Node2D node2d) node2d.Visible = false;
            node.ProcessMode = ProcessModeEnum.Disabled;

            pool.InactiveContainer.AddChild(node);
            pool.Inactive.Enqueue(node);
        }

        public int ReleaseActiveInGroup(string groupName) {
            if (string.IsNullOrWhiteSpace(groupName)) return 0;
            PurgeInvalidActive();
            var matches = new List<Node>();
            foreach (Pool pool in _pools.Values) {
                foreach (Node node in pool.Active) {
                    if (node.IsInGroup(groupName)) matches.Add(node);
                }
            }
            foreach (Node node in matches) Release(node);
            return matches.Count;
        }

        /// <summary>
        /// Hands back every active pooled node parented under <paramref name="root"/>
        /// (or <paramref name="root"/> itself). Scene owners call this from
        /// <c>_ExitTree</c>: a pooled node parented to a scene that is being freed
        /// would otherwise be destroyed with it and left in the pool's active list as
        /// a dead reference, which poisons every later spawn and release.
        /// Scoping by ancestry rather than by group means one unloading scene can
        /// never hand back another live scene's pooled objects.
        /// </summary>
        public int ReleaseActiveUnder(Node root) {
            if (root == null || !IsInstanceValid(root)) return 0;
            PurgeInvalidActive();
            var matches = new List<Node>();
            foreach (Pool pool in _pools.Values) {
                foreach (Node node in pool.Active) {
                    if (node == root || root.IsAncestorOf(node)) matches.Add(node);
                }
            }
            foreach (Node node in matches) Release(node);
            return matches.Count;
        }

        /// <summary>
        /// Drops active entries whose node was freed behind the pool's back. Without
        /// this a single externally freed node makes every subsequent group/ancestry
        /// sweep throw <see cref="ObjectDisposedException"/>, and permanently consumes
        /// a slot of the pool's capacity.
        /// </summary>
        public int PurgeInvalidActive() {
            int purged = 0;
            foreach (Pool pool in _pools.Values) {
                for (int index = pool.Active.Count - 1; index >= 0; index--) {
                    if (IsInstanceValid(pool.Active[index])) continue;
                    pool.Active.RemoveAt(index);
                    purged++;
                }
            }
            return purged;
        }

        public IReadOnlyList<Node> GetActiveNodes(string poolID) {
            if (string.IsNullOrWhiteSpace(poolID) || !_poolsByID.TryGetValue(poolID.Trim(), out Pool pool)) {
                return Array.Empty<Node>();
            }
            return pool.Active.ToArray();
        }

        private static Node CreateInstance(PackedScene template) {
            return template.Instantiate();
        }

        public void ClearPool(PackedScene template) {
            if (template == null) return;
            string key = GetTemplateKey(template);
            if (!_pools.TryGetValue(key, out var pool)) return;

            foreach (var node in pool.Active) {
                if (IsInstanceValid(node)) node.QueueFree();
            }
            pool.Active.Clear();

            while (pool.Inactive.Count > 0) {
                pool.Inactive.Dequeue().QueueFree();
            }
            pool.InactiveContainer.QueueFree();
            _pools.Remove(key);
            RemovePoolAliases(pool);
        }

        public void ClearAllPools() {
            foreach (var kvp in _pools) {
                foreach (var node in kvp.Value.Active) {
                    if (IsInstanceValid(node)) node.QueueFree();
                }
                kvp.Value.Active.Clear();
                while (kvp.Value.Inactive.Count > 0) {
                    kvp.Value.Inactive.Dequeue().QueueFree();
                }
                kvp.Value.InactiveContainer.QueueFree();
            }
            _pools.Clear();
            _poolsByID.Clear();
        }

        public bool IsRegistered(PackedScene template) =>
            template != null && _pools.ContainsKey(GetTemplateKey(template));

        public bool IsRegistered(string poolID) =>
            !string.IsNullOrWhiteSpace(poolID) && _poolsByID.ContainsKey(poolID.Trim());

        public PoolStats? GetStats(PackedScene template) {
            if (template == null || !_pools.TryGetValue(GetTemplateKey(template), out Pool pool)) return null;
            return new PoolStats(pool.Active.Count, pool.Inactive.Count, pool.MaxCapacity, pool.OverflowPolicy);
        }

        public PoolStats? GetStats(string poolID) {
            if (string.IsNullOrWhiteSpace(poolID) || !_poolsByID.TryGetValue(poolID.Trim(), out Pool pool)) return null;
            return new PoolStats(pool.Active.Count, pool.Inactive.Count, pool.MaxCapacity, pool.OverflowPolicy);
        }

        private void RemovePoolAliases(Pool pool) {
            var aliases = new List<string>();
            foreach ((string id, Pool candidate) in _poolsByID) {
                if (ReferenceEquals(candidate, pool)) aliases.Add(id);
            }
            foreach (string id in aliases) _poolsByID.Remove(id);
        }

        private static string GetTemplateKey(PackedScene template) =>
            string.IsNullOrWhiteSpace(template.ResourcePath)
                ? $"instance:{template.GetInstanceId()}"
                : template.ResourcePath;
    }
}
