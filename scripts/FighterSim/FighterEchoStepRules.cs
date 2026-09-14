using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// V7.6 Echo Step destination policy (Package 11 A1c), the deterministic half.
    /// <c>docs/design-contracts/TEMPORAL_STATE_CONTRACT.md</c> "Echo Step: exact
    /// destination, validated twice":
    ///
    /// <list type="number">
    /// <item>the destination is the <b>exact</b> <c>t - 30</c> ring sample, never a
    /// nearest-sample approximation and never interpolated;</item>
    /// <item>it is validated at activation — an invalid target is refused
    /// <b>before</b> meter or cooldown is spent;</item>
    /// <item>the same target is rechecked when the 8-frame wind-up completes — if it
    /// became blocked the teleport is cancelled and the committed cost is
    /// <b>retained</b>. No nearby substitute, no refund, no added immunity.</item>
    /// </list>
    ///
    /// <para><b>What "blocked" means in this simulation.</b> The sim's terrain model
    /// is exactly four rules, all applied to the fighter's position point in
    /// <c>FighterMovementSystem</c>: x is clamped to the side walls, y is clamped to
    /// the ceiling, y below the bottom blast zone is a stock loss, and y snaps up to
    /// the floor plane (<c>y = 0</c>) wherever <see cref="FighterStageGeometry.HasFloorSupport"/>
    /// is true. The clearance predicate is the mirror image of those four rules, so
    /// a destination this class accepts is a position the fighter could legally
    /// occupy and one it rejects is a position the movement system would have to
    /// correct.
    /// </para>
    ///
    /// <para><b>Why any y below the floor plane is rejected.</b> On a Sealed stage
    /// that is the inside of the solid floor — a solid overlap. On one of A9's Open
    /// stages, at an x with no floor segment, it is the interior of an authored pit,
    /// whose only exit is the blast zone: the contract's "authored kill region".
    /// Airborne destinations <em>above</em> the floor plane stay legal everywhere,
    /// which is what "airborne destinations remain legal; support is not required"
    /// means in practice — a jump arc is a valid Echo Step target.</para>
    /// </summary>
    public static class FighterEchoStepRules {

        /// <summary>
        /// The floor plane. <c>FighterMovementSystem</c> snaps a grounded fighter to
        /// <c>y = 0</c>; nothing below it is ever a position a fighter holds except
        /// while falling into a pit or through the legacy arena's drop-through
        /// ground, and both of those lead only to the blast zone.
        /// </summary>
        private static readonly FP64 FloorPlaneY = FP64.Zero;

        /// <summary>
        /// True when <paramref name="destination"/> is a position the fighter's full
        /// body may occupy on <paramref name="geometry"/>: inside the side walls,
        /// at or below the ceiling, above the bottom blast zone, and not inside the
        /// solid floor or an authored pit.
        /// </summary>
        public static bool IsDestinationClear(FighterStageGeometry geometry, in FPVector2 destination) {
            FighterStageGeometry stage = geometry ?? FighterStageGeometry.Default;
            // Side walls are solid: the movement system clamps into them, so a
            // destination outside them is inside terrain. Exactly ON a wall is the
            // same position a fighter is clamped to, so it stays legal.
            if (destination.x < stage.LeftWall || destination.x > stage.RightWall) return false;
            // The ceiling is solid and the movement system clamps down to it.
            if (destination.y > stage.Ceiling) return false;
            // The blast zone is an authored kill region. Subsumed by the floor-plane
            // rule on every authored stage, but stated explicitly so a future stage
            // with a blast zone above the floor plane still refuses.
            if (destination.y <= stage.BottomBlastZone) return false;
            // Solid main floor, or the interior of an authored pit. See the class
            // remarks: both are refusals, for different reasons in the contract.
            if (destination.y < FloorPlaneY) return false;
            return true;
        }

        /// <summary>
        /// Whether the ring holds a real <c>t - 30</c> sample yet.
        /// <c>ValidCount</c> counts samples written since the last generation reset;
        /// spawn-filled slots are never counted, so the fighter genuinely has to
        /// live 30 ticks past a spawn, a respawn, a stock loss or Sudden Death setup
        /// before an exact 30-frame-old position exists.
        /// </summary>
        public static bool HasLookbackSample(int validCount) =>
            validCount >= FighterEchoStepRing.SampleCount;
    }

    /// <summary>
    /// Frame-level operations on the five-component Echo Step ring bank (Package 11
    /// A1c). Kept beside the clearance predicate so every rule about the history —
    /// when it is sampled, when a generation ends, and where <c>t - 30</c> lives —
    /// is in one file.
    /// </summary>
    public static class FighterEchoStepHistory {

        /// <summary>
        /// Opens a new history generation at <paramref name="position"/>: every slot
        /// is filled with that coordinate, the head returns to zero, the armed
        /// wind-up is dropped, and <c>ValidCount</c> goes to <b>0</b> — filled slots
        /// are storage, never history. Called at spawn, at a stock loss (through the
        /// <c>LifeEpoch</c> mismatch the sampler detects) and at Sudden Death setup.
        /// </summary>
        public static void Reset(ref Frame frame, EntityRef entity, in FPVector2 position, int lifeEpoch) {
            ref FighterEchoStepRing0Component slice0 = ref frame.Get<FighterEchoStepRing0Component>(entity);
            ref FighterEchoStepRing1Component slice1 = ref frame.Get<FighterEchoStepRing1Component>(entity);
            ref FighterEchoStepRing2Component slice2 = ref frame.Get<FighterEchoStepRing2Component>(entity);
            ref FighterEchoStepRing3Component slice3 = ref frame.Get<FighterEchoStepRing3Component>(entity);
            ref FighterEchoStepRing4Component slice4 = ref frame.Get<FighterEchoStepRing4Component>(entity);
            for (int slot = 0; slot < FighterEchoStepRing.SampleCount; slot++) {
                FighterEchoStepRing.Write(
                    ref slice0, ref slice1, ref slice2, ref slice3, ref slice4, slot, in position);
            }
            slice0.Head = 0;
            slice0.ValidCount = 0;
            slice0.Generation++;
            slice0.LifeEpoch = lifeEpoch;
            slice0.Armed = 0;
            slice0.ActivationTick = 0;
        }

        /// <summary>
        /// Records this tick's position. Called once per fighter per authoritative
        /// 60 Hz tick, at the very top of the movement loop — <b>before</b> the
        /// hitstop short-circuit, before action input and before movement — so the
        /// ring really is one consecutive sample per tick and "t - 30" really is
        /// thirty ticks. An advancing hitstop tick records the same position, which
        /// is what the contract asks for.
        ///
        /// <para>A <paramref name="lifeEpoch"/> that disagrees with the stored one
        /// opens a new generation first: that is how a stock loss, whose chokepoint
        /// has no <c>Frame</c> in hand, still resets the history.</para>
        /// </summary>
        public static void Sample(
            ref Frame frame, EntityRef entity, in FPVector2 position, int lifeEpoch) {
            ref FighterEchoStepRing0Component head = ref frame.Get<FighterEchoStepRing0Component>(entity);
            if (head.LifeEpoch != lifeEpoch) {
                Reset(ref frame, entity, in position, lifeEpoch);
            }
            ref FighterEchoStepRing0Component slice0 = ref frame.Get<FighterEchoStepRing0Component>(entity);
            ref FighterEchoStepRing1Component slice1 = ref frame.Get<FighterEchoStepRing1Component>(entity);
            ref FighterEchoStepRing2Component slice2 = ref frame.Get<FighterEchoStepRing2Component>(entity);
            ref FighterEchoStepRing3Component slice3 = ref frame.Get<FighterEchoStepRing3Component>(entity);
            ref FighterEchoStepRing4Component slice4 = ref frame.Get<FighterEchoStepRing4Component>(entity);
            FighterEchoStepRing.Write(
                ref slice0, ref slice1, ref slice2, ref slice3, ref slice4, slice0.Head, in position);
            slice0.Head = FighterEchoStepRing.Wrap(slice0.Head + 1);
            slice0.LatestTick++;
            if (slice0.ValidCount < FighterEchoStepRing.SampleCount) slice0.ValidCount++;
        }

        /// <summary>
        /// The exact <c>t - 30</c> sample, or false when the generation has not yet
        /// produced one.
        ///
        /// <para>No search and no rounding: with 31 slots and one write per tick,
        /// the slot the head points at (the next one to overwrite) <em>is</em>
        /// <c>t - 30</c>, because <c>head - 1 - 30 == head (mod 31)</c>.</para>
        /// </summary>
        public static bool TryGetLookback(ref Frame frame, EntityRef entity, out FPVector2 sample) {
            ref readonly FighterEchoStepRing0Component slice0 =
                ref frame.GetReadOnly<FighterEchoStepRing0Component>(entity);
            sample = FPVector2.Zero;
            if (!FighterEchoStepRules.HasLookbackSample(slice0.ValidCount)) return false;
            ref readonly FighterEchoStepRing1Component slice1 =
                ref frame.GetReadOnly<FighterEchoStepRing1Component>(entity);
            ref readonly FighterEchoStepRing2Component slice2 =
                ref frame.GetReadOnly<FighterEchoStepRing2Component>(entity);
            ref readonly FighterEchoStepRing3Component slice3 =
                ref frame.GetReadOnly<FighterEchoStepRing3Component>(entity);
            ref readonly FighterEchoStepRing4Component slice4 =
                ref frame.GetReadOnly<FighterEchoStepRing4Component>(entity);
            sample = FighterEchoStepRing.Read(
                in slice0, in slice1, in slice2, in slice3, in slice4, slice0.Head);
            return true;
        }
    }
}
