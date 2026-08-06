using Godot;
using System;

namespace FTT.Core {

    public partial class PerformanceBaselineRunner : Node {
        [Export] public string[] TargetScenePaths = {
            "res://scenes/campaign/Level_00_Tutorial.tscn",
            "res://scenes/campaign/Level_01_Florence.tscn",
            "res://scenes/arenas/TestArena.tscn"
        };
        [Export(PropertyHint.Range, "1,600,1")] public int WarmUpFrames = 60;
        [Export(PropertyHint.Range, "1,1200,1")] public int SampleFrames = 120;

        private Node _target;
        private int _targetIndex = -1;
        private int _frame;
        private int _samples;
        private double _fpsTotal;
        private double _frameMsTotal;
        private double _processMsTotal;
        private double _physicsMsTotal;
        private double _drawCallsTotal;
        private double _primitivesTotal;
        private long _maxManagedAllocationDelta;
        private long _previousAllocatedBytes;

        private static readonly Performance.Monitor FpsMonitor = Enum.Parse<Performance.Monitor>("TimeFps");
        private static readonly Performance.Monitor ProcessMonitor = Enum.Parse<Performance.Monitor>("TimeProcess");
        private static readonly Performance.Monitor PhysicsMonitor = Enum.Parse<Performance.Monitor>("TimePhysicsProcess");
        private static readonly Performance.Monitor StaticMemoryMonitor = Enum.Parse<Performance.Monitor>("MemoryStatic");
        private static readonly Performance.Monitor DrawCallsMonitor = Enum.Parse<Performance.Monitor>("RenderTotalDrawCallsInFrame");
        private static readonly Performance.Monitor PrimitivesMonitor = Enum.Parse<Performance.Monitor>("RenderTotalPrimitivesInFrame");

        public override void _Ready() {
            CallDeferred(MethodName.LoadNextTarget);
        }

        public override void _Process(double delta) {
            if (_target == null) return;
            _frame++;

            long allocatedBytes = GC.GetTotalAllocatedBytes(false);
            if (_frame > WarmUpFrames) {
                _samples++;
                _fpsTotal += ReadMonitor(FpsMonitor);
                _frameMsTotal += delta * 1000.0;
                _processMsTotal += ReadMonitor(ProcessMonitor);
                _physicsMsTotal += ReadMonitor(PhysicsMonitor);
                _drawCallsTotal += ReadMonitor(DrawCallsMonitor);
                _primitivesTotal += ReadMonitor(PrimitivesMonitor);
                _maxManagedAllocationDelta = Math.Max(_maxManagedAllocationDelta, allocatedBytes - _previousAllocatedBytes);
            }
            _previousAllocatedBytes = allocatedBytes;

            if (_frame >= WarmUpFrames + SampleFrames) FinishTarget();
        }

        private void LoadNextTarget() {
            _targetIndex++;
            if (_targetIndex >= TargetScenePaths.Length) {
                GetTree().Quit();
                return;
            }

            PackedScene scene = ResourceLoader.Load<PackedScene>(TargetScenePaths[_targetIndex]);
            if (scene == null) {
                GD.PushError($"PERF_BASELINE missing scene {TargetScenePaths[_targetIndex]}");
                CallDeferred(MethodName.LoadNextTarget);
                return;
            }

            _target = scene.Instantiate();
            AddChild(_target);
            ResetSamples();
        }

        private void FinishTarget() {
            TreeCounts counts = CountTree(_target);
            double divisor = Math.Max(1, _samples);
            GD.Print(
                $"PERF_BASELINE,{TargetScenePaths[_targetIndex]}," +
                $"fps={_fpsTotal / divisor:F2}," +
                $"frame_ms={_frameMsTotal / divisor:F4}," +
                $"process_ms={_processMsTotal / divisor:F4}," +
                $"physics_ms={_physicsMsTotal / divisor:F4}," +
                $"managed_alloc_max_bytes={_maxManagedAllocationDelta}," +
                $"engine_static_memory_bytes={(long)ReadMonitor(StaticMemoryMonitor)}," +
                $"managed_heap_bytes={GC.GetTotalMemory(false)}," +
                $"draw_calls={_drawCallsTotal / divisor:F2}," +
                $"primitives={_primitivesTotal / divisor:F2}," +
                $"nodes={counts.Nodes}," +
                $"animated_sprites={counts.AnimatedSprites}," +
                $"active_particles={counts.ActiveParticles}");

            RemoveChild(_target);
            _target.Free();
            _target = null;
            CallDeferred(MethodName.LoadNextTarget);
        }

        private void ResetSamples() {
            _frame = 0;
            _samples = 0;
            _fpsTotal = 0;
            _frameMsTotal = 0;
            _processMsTotal = 0;
            _physicsMsTotal = 0;
            _drawCallsTotal = 0;
            _primitivesTotal = 0;
            _maxManagedAllocationDelta = 0;
            _previousAllocatedBytes = GC.GetTotalAllocatedBytes(false);
        }

        private static double ReadMonitor(Performance.Monitor monitor) => Performance.GetMonitor(monitor);

        private static TreeCounts CountTree(Node root) {
            var result = new TreeCounts { Nodes = 1 };
            if (root is AnimatedSprite2D animatedSprite && animatedSprite.IsVisibleInTree() && animatedSprite.IsPlaying()) {
                result.AnimatedSprites++;
            }
            if (root is GpuParticles2D gpu && gpu.IsVisibleInTree() && gpu.Emitting) result.ActiveParticles += gpu.Amount;
            if (root is CpuParticles2D cpu && cpu.IsVisibleInTree() && cpu.Emitting) result.ActiveParticles += cpu.Amount;
            foreach (Node child in root.GetChildren()) result += CountTree(child);
            return result;
        }

        private struct TreeCounts {
            public int Nodes;
            public int AnimatedSprites;
            public int ActiveParticles;

            public static TreeCounts operator +(TreeCounts left, TreeCounts right) => new() {
                Nodes = left.Nodes + right.Nodes,
                AnimatedSprites = left.AnimatedSprites + right.AnimatedSprites,
                ActiveParticles = left.ActiveParticles + right.ActiveParticles
            };
        }
    }
}
