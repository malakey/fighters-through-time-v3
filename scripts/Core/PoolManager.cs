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

    [GlobalClass]
    public partial class PoolDefinition : Resource {
        [Export] public PackedScene SceneTemplate;
        [Export] public int WarmUpCount = 10;
        [Export] public int MaxCapacity = 50;
        [Export] public PoolOverflowPolicy OverflowPolicy = PoolOverflowPolicy.Grow;
    }

    [GlobalClass]
    public partial class ScenePoolConfig : Resource {
        [Export] public PoolDefinition[] PoolDefinitions;
    }

    public partial class PooledNode : Node2D {
        public PackedScene SceneOrigin { get; internal set; }

        public void ReturnToPool() {
            PoolManager.Instance.Release(this);
        }
    }

    public partial class PoolManager : Node {
        public static PoolManager Instance { get; private set; }

        private class Pool {
            public PackedScene Template;
            public Queue<Node> Inactive = new();
            public List<Node> Active = new();
            public int MaxCapacity;
            public PoolOverflowPolicy OverflowPolicy;
            public Node InactiveContainer;
        }

        private readonly Dictionary<string, Pool> _pools = new();
        private Node _poolRoot;

        public override void _Ready() {
            Instance = this;
            _poolRoot = new Node();
            _poolRoot.Name = "PoolRoot";
            AddChild(_poolRoot);
        }

        public void RegisterPool(PackedScene template, int warmUpCount, int maxCapacity, PoolOverflowPolicy overflowPolicy) {
            string key = template.ResourcePath;
            if (_pools.ContainsKey(key)) return;

            var pool = new Pool {
                Template = template,
                MaxCapacity = maxCapacity,
                OverflowPolicy = overflowPolicy
            };

            pool.InactiveContainer = new Node();
            pool.InactiveContainer.Name = $"Pool_{System.IO.Path.GetFileNameWithoutExtension(key)}";
            _poolRoot.AddChild(pool.InactiveContainer);

            for (int i = 0; i < warmUpCount; i++) {
                var node = CreateInstance(template);
                node.ProcessMode = ProcessModeEnum.Disabled;
                pool.InactiveContainer.AddChild(node);
                if (node is Node2D node2D) node2D.Visible = false;
                pool.Inactive.Enqueue(node);
            }

            _pools[key] = pool;
        }

        public void WarmFromConfig(ScenePoolConfig config) {
            if (config?.PoolDefinitions == null) return;
            foreach (var def in config.PoolDefinitions) {
                if (def?.SceneTemplate == null) continue;
                RegisterPool(def.SceneTemplate, def.WarmUpCount, def.MaxCapacity, def.OverflowPolicy);
            }
        }

        public Node Spawn(PackedScene template, Vector2 position, Node parent = null) {
            string key = template.ResourcePath;
            if (!_pools.TryGetValue(key, out var pool)) {
                RegisterPool(template, 1, 50, PoolOverflowPolicy.Grow);
                pool = _pools[key];
            }

            Node node;
            if (pool.Inactive.Count > 0) {
                node = pool.Inactive.Dequeue();
                pool.InactiveContainer.RemoveChild(node);
            } else {
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
                            node = pool.Inactive.Dequeue();
                            pool.InactiveContainer.RemoveChild(node);
                        } else {
                            node = CreateInstance(template);
                        }
                        break;
                    default: // Grow
                        node = CreateInstance(template);
                        break;
                }
            }

            var targetParent = parent ?? GetTree().CurrentScene;
            targetParent.AddChild(node);

            if (node is Node2D node2d) {
                node2d.GlobalPosition = position;
                node2d.Visible = true;
            }
            node.ProcessMode = ProcessModeEnum.Inherit;

            if (node is IPoolable poolable) poolable.OnSpawn();
            if (node is PooledNode pooledNode) pooledNode.SceneOrigin = template;

            pool.Active.Add(node);
            return node;
        }

        public void Release(Node node) {
            if (node == null) return;

            string key = null;
            if (node is PooledNode pooledNode && pooledNode.SceneOrigin != null) {
                key = pooledNode.SceneOrigin.ResourcePath;
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

            if (node is IPoolable poolable) poolable.OnDespawn();

            pool.Active.Remove(node);
            node.GetParent()?.RemoveChild(node);

            if (node is Node2D node2d) node2d.Visible = false;
            node.ProcessMode = ProcessModeEnum.Disabled;

            pool.InactiveContainer.AddChild(node);
            pool.Inactive.Enqueue(node);
        }

        private static Node CreateInstance(PackedScene template) {
            return template.Instantiate();
        }

        public void ClearPool(PackedScene template) {
            string key = template.ResourcePath;
            if (!_pools.TryGetValue(key, out var pool)) return;

            foreach (var node in pool.Active) node.QueueFree();
            pool.Active.Clear();

            while (pool.Inactive.Count > 0) {
                pool.Inactive.Dequeue().QueueFree();
            }
        }

        public void ClearAllPools() {
            foreach (var kvp in _pools) {
                foreach (var node in kvp.Value.Active) node.QueueFree();
                kvp.Value.Active.Clear();
                while (kvp.Value.Inactive.Count > 0) {
                    kvp.Value.Inactive.Dequeue().QueueFree();
                }
            }
            _pools.Clear();
        }
    }
}
