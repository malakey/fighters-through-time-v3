using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.FighterSim {

    // The lingering-ability kit pass (KL-SIM, 2026-10-31): the Fighter driver's half of the construct
    // charges and the zone detonations. PRESENTATION ONLY - read-only over the sim's components.
    //
    // * A charge pip row sits on a construct proxy's HP bar (coil 3, turret 4, nest 3).
    // * A synced strike, a discharge burst and a fizzle all show as a drop in a construct's charges
    //   (303 RemainingAttacks), so the strike VFX is edge-detected on that count going BELOW the lowest
    //   value already presented for the construct (identity dedupe by the sim's unique entity ID), the
    //   Defy ring's pattern: a rollback correction that raises the count and re-spends it replays
    //   nothing, and the first sight of a construct (a rematch, a restored snapshot) presents nothing.
    // * A Rift collapse shows once per rift entity, on the first sight of a positive
    //   CollapseFramesRemaining (a finisher or an E=mc2 burst; the burst's own impact is the kit's).
    // The effect is the shared impact burst (the wall bounce's VfxImpact scene).
    public partial class FighterSimulationDriver {

        private const string LingeringImpactScenePath = "res://scenes/vfx/VfxImpact.tscn";
        private const float ChargePipSize = 5f;
        private const float ChargePipGap = 2f;
        private const float ChargePipRiseAboveBar = 12f;

        private readonly Dictionary<int, int> _lowestPresentedCharges = new();
        private readonly HashSet<int> _presentedCollapses = new();
        private FighterSimulation _lingeringSimulation;
        // Review round 1 (finding 7): the per-owner synced-strike count (323 ActionFlags bits 16-19) is the
        // presentation edge for a strike whose construct was destroyed by its last charge.
        private const int StrikeCounterRing = 16;
        private readonly StrikeCounterWatch[] _strikeWatches = { new(), new() };
        private readonly bool[] _chargeEdgePlayedForOwner = new bool[2];
        private PackedScene _lingeringImpactScene;

        /// <summary>Per construct proxy, once per sync: the charge pips and the strike edge.</summary>
        private void SyncLingeringProxy(ColorRect proxy, in FighterPersistentObjectComponent persistent) {
            ResetLingeringMemoryIfNewSimulation();
            int maxCharges = FighterConstructCharges.InitialCharges(persistent.ObjectTypeID);
            if (maxCharges <= 0) return;
            int charges = persistent.RemainingAttacks < 0 ? 0 : persistent.RemainingAttacks;
            ConfigureChargePips(proxy, charges, maxCharges);
            if (!_lowestPresentedCharges.TryGetValue(persistent.EntityID, out int lowest)) {
                _lowestPresentedCharges[persistent.EntityID] = charges;
                return;
            }
            if (charges >= lowest) return;
            _lowestPresentedCharges[persistent.EntityID] = charges;
            if (persistent.OwnerPlayerID is 0 or 1) _chargeEdgePlayedForOwner[persistent.OwnerPlayerID] = true;
            PlayLingeringImpact(proxy.GlobalPosition + proxy.Size / 2f);
        }

        /// <summary>Once per sync after the zone proxies: a rift that began to collapse shows its burst once.</summary>
        private void SyncLingeringZones() {
            ResetLingeringMemoryIfNewSimulation();
            foreach (FighterZoneComponent zone in _zones) {
                if (!FighterRiftRules.IsCollapsing(in zone)) continue;
                if (!_presentedCollapses.Add(zone.EntityID)) continue;
                if (!_zoneProxies.TryGetValue(zone.EntityID, out ColorRect proxy)) continue;
                PlayLingeringImpact(proxy.GlobalPosition + proxy.Size / 2f);
            }
            SyncStrikeCounters();
        }

        /// <summary>
        /// The wrapping strike counter's presentation edge, once per owner per sync after the proxies.
        /// A short forward step presents the strike at the opponent unless the construct's charge edge
        /// already showed it this sync; a lowered or unchanged counter shows nothing, and the same
        /// value re-reached after a rollback correction is not replayed.
        /// </summary>
        private void SyncStrikeCounters() {
            for (int owner = 0; owner < 2; owner++) {
                if (!Simulation.TryGetFighterActionState(owner, out FighterActionStateComponent action)) continue;
                bool fresh = _strikeWatches[owner].Observe(FighterSyncedStrikeRules.StrikeCount(in action));
                bool alreadyShown = _chargeEdgePlayedForOwner[owner];
                _chargeEdgePlayedForOwner[owner] = false;
                if (!fresh || alreadyShown) continue;
                if (!Simulation.TryGetFighter(owner == 0 ? 1 : 0, out FighterStateComponent target)) continue;
                PlayLingeringImpact(new Vector2(
                    target.Position.x.ToFloat() * PixelsPerUnit,
                    -target.Position.y.ToFloat() * PixelsPerUnit - 60f));
            }
        }

        private void ResetLingeringMemoryIfNewSimulation() {
            if (ReferenceEquals(_lingeringSimulation, Simulation)) return;
            _lingeringSimulation = Simulation;
            _lowestPresentedCharges.Clear();
            _presentedCollapses.Clear();
            _strikeWatches[0].Reset();
            _strikeWatches[1].Reset();
            _chargeEdgePlayedForOwner[0] = _chargeEdgePlayedForOwner[1] = false;
        }

        private void PlayLingeringImpact(Vector2 position) {
            _lingeringImpactScene ??= ResourceLoader.Load<PackedScene>(LingeringImpactScenePath);
            VfxEmitter.EmitScene(_lingeringImpactScene, position, GetParent());
        }

        /// <summary>The pip row above a construct's HP bar: one square per charge, gold when held, dark when spent.</summary>
        internal static void ConfigureChargePips(ColorRect proxy, int charges, int maxCharges) {
            Control row = proxy.GetNodeOrNull<Control>("ChargePips");
            if (row == null) {
                row = new Control { Name = "ChargePips", MouseFilter = Control.MouseFilterEnum.Ignore };
                proxy.AddChild(row);
            }
            while (row.GetChildCount() < maxCharges) {
                row.AddChild(new ColorRect { Name = $"Pip{row.GetChildCount()}", Size = new Vector2(ChargePipSize, ChargePipSize) });
            }
            row.Position = new Vector2(0f, -ChargePipRiseAboveBar - ChargePipSize);
            for (int index = 0; index < row.GetChildCount(); index++) {
                if (row.GetChild(index) is not ColorRect pip) continue;
                pip.Visible = index < maxCharges;
                pip.Position = new Vector2(index * (ChargePipSize + ChargePipGap), 0f);
                pip.Color = index < charges ? UIPalette.Accent : new Color(0.15f, 0.15f, 0.2f, 0.7f);
            }
        }
    }

    /// <summary>
    /// Review round 1 (finding 7): edge detection for a wrapping presentation counter with identity
    /// dedupe (the Defy counter's pattern). <see cref="Observe"/> returns true once per genuinely new
    /// step: the first sight baselines, an unchanged or lowered count shows nothing, and the value
    /// already presented, re-reached after a backward step (a rollback correction), is not replayed.
    /// </summary>
    internal sealed class StrikeCounterWatch {
        private int _previous = -1;
        private int _lastPresented = -1;
        private bool _wentBackSincePresented;

        public void Reset() { _previous = -1; _lastPresented = -1; _wentBackSincePresented = false; }

        public bool Observe(int count, int ring = 16) {
            int previous = _previous;
            _previous = count;
            if (previous < 0 || count == previous) return false;
            if (!FighterSimulationDriver.IsForwardCounterStep(previous, count, ring)) {
                if (_lastPresented >= 0) _wentBackSincePresented = true;
                return false;
            }
            if (_wentBackSincePresented && count == _lastPresented) {
                _wentBackSincePresented = false;
                return false;
            }
            _lastPresented = count;
            _wentBackSincePresented = false;
            return true;
        }
    }
}
