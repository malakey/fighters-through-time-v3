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
        private readonly int[] _strikeLastStocks = { -1, -1 };
        private bool _strikeSuddenDeathSeen;
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
            if (!_strikeSuddenDeathSeen && Simulation.GetMatchState().SuddenDeathActive == 1) {
                _strikeSuddenDeathSeen = true;
                _strikeWatches[0].Reset();
                _strikeWatches[1].Reset();
            }
            for (int owner = 0; owner < 2; owner++) {
                if (!Simulation.TryGetFighterActionState(owner, out FighterActionStateComponent action)) continue;
                // A stock loss or the respawn hold zeroes 323: a lifecycle reset, never a strike.
                if (Simulation.TryGetFighter(owner, out FighterStateComponent self)) {
                    bool lostStock = _strikeLastStocks[owner] >= 0 && self.Stocks < _strikeLastStocks[owner];
                    _strikeLastStocks[owner] = self.Stocks;
                    if (lostStock || self.RespawnFramesRemaining > 0) {
                        _strikeWatches[owner].Reset();
                        _chargeEdgePlayedForOwner[owner] = false;
                        continue;
                    }
                }
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
            _strikeLastStocks[0] = _strikeLastStocks[1] = -1;
            _strikeSuddenDeathSeen = false;
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
    /// Edge detection for a wrapping presentation counter with a presentation HIGH-WATER mark (review
    /// round 2). The counter is tracked unwrapped by its signed shortest step; <see cref="Observe"/>
    /// returns true only when it passes the highest position already presented, so a correction that
    /// crosses several counts (0,1,2,3,1,2,3) replays nothing, a wrap (15 to 0) still counts, the first
    /// sight baselines, and a lifecycle reset (<see cref="Reset"/>: a stock loss, a round or Sudden Death
    /// zeroing 323) re-baselines instead of reading as a forward wrap.
    /// </summary>
    internal sealed class StrikeCounterWatch {
        private bool _seen;
        private int _previous;
        private int _position;
        private int _highWater;

        public void Reset() { _seen = false; }

        public bool Observe(int count, int ring = 16) {
            if (!_seen) {
                _seen = true;
                _previous = count;
                _position = _highWater = 0;
                return false;
            }
            int half = ring / 2;
            int step = (((count - _previous + half) % ring) + ring) % ring - half;
            _previous = count;
            _position += step;
            if (_position <= _highWater) return false;
            _highWater = _position;
            return true;
        }
    }
}
