using System;
using System.Threading.Tasks;
using Godot;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W9 — the root cause of the <c>EnemyControllerTests</c>
/// order-dependent flake, and its fix.
///
/// <para>
/// A synchronous test drives a body by calling <c>_PhysicsProcess(Step)</c>
/// directly. Every state timer in that step uses the fixed 1/60 the test passes,
/// but <see cref="CharacterBody2D.MoveAndSlide"/> takes no delta: Godot reads
/// <c>get_physics_process_delta_time()</c> only while the engine is inside a
/// physics frame, and <b>otherwise falls back to the idle frame's
/// <c>get_process_delta_time()</c></b> — the wall-clock length of whatever idle
/// frame the test runner happened to be in. Measured across one full run (a
/// diagnostic hook in front of every <c>EnemyControllerTests</c> case, 2026-09-27)
/// that value ranged from <b>0.0003 s to 0.0174 s</b> — a 58× spread decided by
/// how busy the runner's previous frame was, i.e. by test order. At 0.0003 s a
/// 252 px/s knight covers 0.08 px per "frame", so a walk-back bounded at 300
/// steps stops short (<c>expected Patrol got Returning</c>) and a hovering drone
/// never reaches its stand-off band inside its 2,000-step cap. In isolation the
/// frames are quieter and every case passes — exactly the reported signature.
/// No leaked player, grouped node or static registry was involved: the tree
/// contained only autoloads and pooled presentation nodes at every case.
/// </para>
///
/// <para>
/// The fix runs a movement-sensitive case's synchronous body <b>inside a real
/// physics frame</b> (from the <c>physics_frame</c> signal, with no continuation
/// hop), so every out-of-band <c>MoveAndSlide</c> uses the fixed 1/60 physics
/// step. Nothing else in the case changes.
/// </para>
/// </summary>
internal static class OutOfBandPhysicsStep {
    /// <summary>The idle delta an out-of-band <c>MoveAndSlide</c> would use right now.</summary>
    public static double CurrentProcessDelta {
        get {
            var tree = (SceneTree)Engine.GetMainLoop();
            return tree.Root.GetProcessDeltaTime();
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> synchronously on the next
    /// <c>physics_frame</c> signal, where <see cref="Engine.IsInPhysicsFrame"/>
    /// is true and <c>MoveAndSlide</c> reads the fixed physics step. The handler
    /// is invoked directly by the emission — never through the synchronization
    /// context — so the body cannot drift into an idle frame. Exceptions,
    /// including GdUnit assertion failures, surface through the returned task.
    /// </summary>
    public static Task RunInPhysicsFrameAsync(Action body) {
        var tree = (SceneTree)Engine.GetMainLoop();
        var completion = new TaskCompletionSource(TaskCreationOptions.None);
        Action handler = null;
        handler = () => {
            tree.PhysicsFrame -= handler;
            try {
                body();
                completion.SetResult();
            } catch (Exception exception) {
                completion.SetException(exception);
            }
        };
        tree.PhysicsFrame += handler;
        return completion.Task;
    }
}
