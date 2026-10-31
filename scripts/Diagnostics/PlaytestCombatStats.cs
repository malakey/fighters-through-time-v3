using System;
using System.Collections.Generic;
using FTT.Combat;

namespace FTT.Diagnostics {

    // =====================================================================
    // D6d (user direction 2026-10-11): the bots' combat telemetry, the
    // Godot-free half. PlaytestTelemetry feeds these from the live world; the
    // trackers themselves read nothing but their arguments, so they are tested
    // without a scene. Diagnostics only: nothing here is gameplay state, and
    // nothing under scripts/FighterSim/ may reference it.
    // =====================================================================

    /// <summary>The three cast slots a bot press is tracked for.</summary>
    public enum PressSlot { Special1 = 0, Special2 = 1, Ultimate = 2 }

    /// <summary>Shared geometry, window lengths and order statistics for the combat telemetry.</summary>
    public static class PlaytestCombatMath {
        /// <summary>
        /// A swing is a combat swing when the nearest foe hurtbox lies within
        /// this gap of the hitbox when the swing opens (about 3.3 units, beyond
        /// any string reach). Others (objective strikes, swings at nothing) are
        /// reported as non-combat.
        /// </summary>
        public const float CombatSwingGapPx = 200f;
        /// <summary>A finisher record with no closing event after this many frames closes as <c>lost</c>.</summary>
        public const int ReapproachGiveUpFrames = 600;
        /// <summary>A finisher target never seen airborne has its carry measured this many frames after the connect.</summary>
        public const int LandingWindowFrames = 90;
        /// <summary>A meter at or below this starts a clean bar-fill window.</summary>
        public const float FillWindowFloor = 1f;

        /// <summary>
        /// Distance between two axis-aligned rectangles given as min/max corners:
        /// 0 when they overlap or touch, else the Euclidean length of the
        /// separating gaps (a corner gap of 3 and 4 is 5).
        /// </summary>
        public static float AabbGap(float ax0, float ay0, float ax1, float ay1,
            float bx0, float by0, float bx1, float by1) {
            float dx = MathF.Max(0f, MathF.Max(bx0 - ax1, ax0 - bx1));
            float dy = MathF.Max(0f, MathF.Max(by0 - ay1, ay0 - by1));
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Horizontal distance from <paramref name="x"/> to the span [min, max]; 0 inside it.</summary>
        public static float HorizontalGap(float x, float minX, float maxX) {
            if (x < minX) return minX - x;
            if (x > maxX) return x - maxX;
            return 0f;
        }

        /// <summary>
        /// The basic-attack family a swing's <c>HitboxID</c> belongs to (GU-BOT):
        /// <c>string</c> (the grounded chain, <c>combo_1..3</c>), <c>air_attack</c>
        /// (the single aerial strike since GU-A, 2026-10-12; it never chains),
        /// <c>up_attack</c>, <c>down_air</c>, else <c>other</c>.
        /// </summary>
        public static string HitVariant(string hitboxID) {
            if (string.IsNullOrEmpty(hitboxID)) return "other";
            if (hitboxID == BasicComboRules.AirAttackHitboxID) return "air_attack";
            if (hitboxID == BasicComboRules.UpAttackHitboxID) return "up_attack";
            if (hitboxID == BasicComboRules.DownAirHitboxID) return "down_air";
            return hitboxID.StartsWith("combo_", StringComparison.Ordinal) ? "string" : "other";
        }

        /// <summary>
        /// The <c>swings.by_hit</c> key: the hit ID, with <c>_air</c> appended for an
        /// airborne swing — except the air strike, which is airborne by definition.
        /// </summary>
        public static string SwingHitKey(string hitboxID, bool air) =>
            (hitboxID ?? "") + (air && hitboxID != BasicComboRules.AirAttackHitboxID ? "_air" : "");

        /// <summary>The lower median <c>sorted[n / 2]</c> (the report's frames-to-kill convention); −1 when empty.</summary>
        public static int Median(List<int> values) {
            if (values == null || values.Count == 0) return -1;
            var sorted = new List<int>(values);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }

        /// <summary>The nearest-rank percentile <c>sorted[min(n − 1, ceil(p/100 · n) − 1)]</c>; −1 when empty.</summary>
        public static int Percentile(List<int> values, int p) {
            if (values == null || values.Count == 0) return -1;
            var sorted = new List<int>(values);
            sorted.Sort();
            int rank = (int)Math.Ceiling(Math.Clamp(p, 0, 100) / 100.0 * sorted.Count) - 1;
            return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
        }

        /// <summary>
        /// One physics tick in seconds. The hero ticks its Special reuse delays (plain seconds,
        /// P3) before it polls a press (spec C §1.4 fact 7), so a delay at or below this at the
        /// snapshot is clear by the poll and is not a <c>reuse_delay</c> refusal.
        /// </summary>
        public const float ReuseTickSeconds = 1f / 60f;

        /// <summary>Float slack so a gauge at 0.99999 pips still pays (the hero's own slack).</summary>
        public const float PipSlack = 0.0001f;

        /// <summary>One pip: the cost of a Special.</summary>
        public const float SpecialCostPips = 1f;

        /// <summary>
        /// The smallest gate reading that still refuses a press: the gate ticks
        /// before the poll too, so a snapshot reading of 1 clears this tick.
        /// </summary>
        public const int GateRefusesFromFrames = 2;

        /// <summary>The refusal labels, in the order <see cref="RefusalConditions"/> reports them.</summary>
        public static readonly string[] RefusalLabels = {
            "hitstop", "time_freeze", "suppressed", "lockout", "dormant", "reuse_delay", "no_momentum", "meter", "executing"
        };

        /// <summary>
        /// C3 (spec C §1.4(d), round 2 point 5): every directly readable condition
        /// that held in a press's pre-update snapshot, in the
        /// <see cref="RefusalLabels"/> order. There is no precedence and no list of
        /// states that poll a press: these are conditions present, not the hero's
        /// own check order, so a refused press usually carries several, and one
        /// with none was refused for a reason that is not directly readable (a
        /// non-polling state, a chain-start tick, a kit rule, the tech lock).
        /// </summary>
        public static List<string> RefusalConditions(in PressSnapshot s) {
            var labels = new List<string>(4);
            bool ultimate = s.Slot == PressSlot.Ultimate;
            if (s.HitstopFrames > 0) labels.Add("hitstop");
            if (s.TimeFrozen) labels.Add("time_freeze");
            if (s.Suppressed) labels.Add("suppressed");
            if (s.GateFrames >= GateRefusesFromFrames) labels.Add("lockout");
            if (!s.SlotUnlocked) labels.Add("dormant");
            if (!ultimate && s.ReuseSeconds > ReuseTickSeconds) labels.Add("reuse_delay");
            if (!ultimate && s.MomentumPips < SpecialCostPips - PipSlack) labels.Add("no_momentum");
            if (ultimate && !s.MeterFull) labels.Add("meter");
            if (s.AbilityExecuting) labels.Add("executing");
            return labels;
        }
    }

    /// <summary>
    /// C3: the hero's directly readable state for one Special 1, Special 2 or
    /// Ultimate press, read on the telemetry tick that registered it — after the
    /// hero's previous update and before the update that consumes the press.
    /// <see cref="GateFrames"/> is the hero's Special gate for a Special and the
    /// ability lockout for the Ultimate (D1: no hit-confirm for the Ultimate).
    /// </summary>
    public struct PressSnapshot {
        public PressSlot Slot;
        public int HitstopFrames;
        /// <summary>The hero's <c>CharacterState</c> name.</summary>
        public string State;
        public bool TimeFrozen;
        public bool Suppressed;
        public int GateFrames;
        public bool SlotUnlocked;
        /// <summary>The slot's 2 s reuse delay left, in seconds (Specials; 0 for the Ultimate).</summary>
        public float ReuseSeconds;
        /// <summary>The Momentum gauge in pips (read for the Specials only).</summary>
        public float MomentumPips;
        /// <summary>The meter is at the cap (read for the Ultimate only).</summary>
        public bool MeterFull;
        /// <summary>That slot's ability node is executing.</summary>
        public bool AbilityExecuting;

        /// <summary>A snapshot in which nothing readable refuses the press.</summary>
        public static PressSnapshot Clear(PressSlot slot, string state = "Idle") => new() {
            Slot = slot,
            State = state,
            SlotUnlocked = true,
            MeterFull = true,
            MomentumPips = BasicComboRules.MomentumMaxPips
        };
    }

    /// <summary>
    /// One active window of the hero's <c>MeleeHitbox</c> (a chain hit, the
    /// air strike, the up-attack or the down-air). Opened by its first contact
    /// or by the first telemetry tick that sees the box active, whichever comes
    /// first; closed by the first tick that sees the box inactive or carrying a
    /// different hit.
    /// </summary>
    public sealed class SwingRecord {
        /// <summary>Telemetry frame the swing opened.</summary>
        public int F;
        /// <summary>The hitbox's <c>HitboxID</c> (<c>combo_1..3</c>, <c>air_attack</c> since GU-A, <c>up_attack</c>, <c>down_air</c>).</summary>
        public string Hit = "";
        /// <summary>The hero was airborne when the swing opened.</summary>
        public bool Air;
        /// <summary>The bot's action label when the swing opened.</summary>
        public string Act = "";
        /// <summary>Pixel gap from the hitbox to the nearest foe hurtbox at open; −1 with no foe or no box.</summary>
        public int Gap = -1;
        /// <summary>The smallest gap to a live foe over every tick the swing stayed open.</summary>
        public int GapMin = -1;
        /// <summary>Horizontal distance from the hero's pivot to that foe hurtbox's near edge at open.</summary>
        public int Piv = -1;
        public string Foe = "";
        /// <summary><c>standard</c> | <c>elite</c> | <c>boss</c> | <c>guardian</c>, or empty with no foe.</summary>
        public string Kind = "";
        /// <summary>That foe's centre lay behind the hero's facing.</summary>
        public bool Behind;
        /// <summary>Contacts the hitbox delivered to a receiver (a hurtbox's <c>TakeDamage</c> ran).</summary>
        public int Contacts;
        /// <summary>Contacts that landed (D1a once group A is in, else damage &gt; 0).</summary>
        public int Landed;
        /// <summary>Contacts that reached a receiver without landing (blocked, warded, a strike surface or prop).</summary>
        public int NoLanding;
        /// <summary>HP the landed contacts removed.</summary>
        public float Damage;
        /// <summary><c>c</c> connect, <c>x</c> contact without landing, <c>w</c> whiff; empty while open; <c>dropped</c> when a hero rebuild discarded it.</summary>
        public string Outcome = "";

        /// <summary>Telemetry-side: instance id of the nearest foe at open (the finisher target).</summary>
        internal ulong TargetId;
        /// <summary>Telemetry-side: that foe's x at open.</summary>
        internal float TargetX;

        public bool IsOpen => Outcome.Length == 0;
        public bool IsCombat => Gap >= 0 && Gap <= PlaytestCombatMath.CombatSwingGapPx;
        public bool IsCounted => Outcome is "c" or "x" or "w";

        /// <summary>Sets <see cref="Outcome"/> from the contact counts (§1.4(a)).</summary>
        public void Close() {
            if (!IsOpen) return;
            Outcome = Landed > 0 ? "c" : Contacts > 0 ? "x" : "w";
        }
    }

    /// <summary>
    /// C3 (spec C §1.4(e)): one active window of a mob's melee attack — an
    /// <c>EnemyController</c> (standard or elite) whose active ability is a
    /// <c>MeleeStrike</c> or a <c>ChargeDash</c>. Bosses and every other archetype
    /// are out of scope. Opened by the first contact on the hero's hurtbox or the
    /// first telemetry tick that sees the active phase; closed by the first tick
    /// that sees another phase or another ability. Spec D's D6c acceptance reads
    /// the whiffs ("mob melee whiffs from stand-off gone").
    /// </summary>
    public sealed class MobSwingRecord {
        /// <summary>Telemetry frame the swing opened.</summary>
        public int F;
        /// <summary>The mob's <c>EnemyID</c> (or node name).</summary>
        public string Foe = "";
        /// <summary><c>standard</c> | <c>elite</c>.</summary>
        public string Kind = "";
        /// <summary>The active ability's <c>AbilityID</c> (or resource name).</summary>
        public string Ability = "";
        /// <summary>Pixel gap from the mob's attack hitbox to the hero's hurtbox at open; −1 with either missing.</summary>
        public int Gap = -1;
        /// <summary>Contacts the attack hitbox delivered to the hero's hurtbox (a block included).</summary>
        public int Contacts;
        /// <summary>The window ended in its recovery; false when a stun, death, freeze, despawn or the end of the run cut it.</summary>
        public bool Natural;
        /// <summary><c>c</c> any contact, <c>w</c> none; empty while open.</summary>
        public string Outcome = "";

        public bool IsOpen => Outcome.Length == 0;

        public void Close(bool natural) {
            if (!IsOpen) return;
            Natural = natural;
            Outcome = Contacts > 0 ? "c" : "w";
        }
    }

    /// <summary>
    /// Counts the bot's Special 1, Special 2 and Ultimate presses. A press is
    /// accepted iff its slot's <c>BaseSpecial.ExecutionSerial</c> advanced
    /// between the tick it was registered (before the hero's update consumed
    /// it) and a later tick; otherwise it was refused. Presses still pending
    /// when the hero is rebuilt or the run ends are unresolved.
    /// </summary>
    public sealed class AbilityPressTracker {
        public sealed class Counts {
            public int Pressed;
            public int Accepted;
            public int Refused;
            public int Unresolved;
            // C3: refusals of presses registered with a snapshot (§1.4(d)).
            /// <summary>Per label, the refused presses whose snapshot held it (a press may count under several).</summary>
            public readonly Dictionary<string, int> RefusedWith = new();
            /// <summary>Per label, the refused presses whose snapshot held exactly that one label.</summary>
            public readonly Dictionary<string, int> RefusedSole = new();
            /// <summary>Refused presses whose snapshot held no readable label.</summary>
            public int RefusedNone;
            /// <summary>Per hero state at the snapshot, the refused presses.</summary>
            public readonly Dictionary<string, int> RefusedState = new();
        }

        private readonly struct Pending {
            public readonly int Frame;
            public readonly PressSlot Slot;
            public readonly int SerialBefore;
            public readonly bool HasSnapshot;
            public readonly PressSnapshot Snapshot;
            public Pending(int frame, PressSlot slot, int serialBefore, bool hasSnapshot, in PressSnapshot snapshot) {
                Frame = frame;
                Slot = slot;
                SerialBefore = serialBefore;
                HasSnapshot = hasSnapshot;
                Snapshot = snapshot;
            }
        }

        public readonly Counts[] SlotCounts = { new Counts(), new Counts(), new Counts() };
        private readonly List<Pending> _pending = new();

        public int PendingCount => _pending.Count;

        public Counts For(PressSlot slot) => SlotCounts[(int)slot];

        /// <summary>Registers a press made on <paramref name="frame"/>, with its slot's serial read before the hero's update. Books counts only.</summary>
        public void Register(int frame, PressSlot slot, int serialBefore) {
            SlotCounts[(int)slot].Pressed++;
            _pending.Add(new Pending(frame, slot, serialBefore, hasSnapshot: false, default));
        }

        /// <summary>
        /// C3: registers a press with the hero's pre-update <paramref name="snapshot"/>.
        /// A refusal also books the snapshot's readable conditions.
        /// </summary>
        public void Register(int frame, PressSlot slot, int serialBefore, in PressSnapshot snapshot) {
            SlotCounts[(int)slot].Pressed++;
            _pending.Add(new Pending(frame, slot, serialBefore, hasSnapshot: true, snapshot));
        }

        /// <summary>Resolves every press registered before <paramref name="frame"/> against the slots' current serials.</summary>
        public void Resolve(int frame, int serialS1, int serialS2, int serialUlt) {
            for (int i = _pending.Count - 1; i >= 0; i--) {
                Pending press = _pending[i];
                if (press.Frame >= frame) continue;
                int now = press.Slot switch {
                    PressSlot.Special1 => serialS1,
                    PressSlot.Special2 => serialS2,
                    _ => serialUlt
                };
                Counts counts = SlotCounts[(int)press.Slot];
                if (now > press.SerialBefore) {
                    counts.Accepted++;
                } else {
                    counts.Refused++;
                    if (press.HasSnapshot) BookRefusal(counts, press.Snapshot);
                }
                _pending.RemoveAt(i);
            }
        }

        private static void BookRefusal(Counts counts, in PressSnapshot snapshot) {
            List<string> labels = PlaytestCombatMath.RefusalConditions(snapshot);
            foreach (string label in labels) Increment(counts.RefusedWith, label);
            if (labels.Count == 1) Increment(counts.RefusedSole, labels[0]);
            else if (labels.Count == 0) counts.RefusedNone++;
            Increment(counts.RefusedState, string.IsNullOrEmpty(snapshot.State) ? "?" : snapshot.State);
        }

        private static void Increment(Dictionary<string, int> table, string key) =>
            table[key] = table.TryGetValue(key, out int value) ? value + 1 : 1;

        /// <summary>Every pending press becomes unresolved (a hero rebuild or the end of the run).</summary>
        public void AbandonPending() {
            foreach (Pending press in _pending) SlotCounts[(int)press.Slot].Unresolved++;
            _pending.Clear();
        }
    }

    /// <summary>
    /// What happens after a landed string finisher (D6a's walk-back question):
    /// how many frames until the hero swings again, how far the target was
    /// carried and where it landed. At most one record is open at a time.
    /// <para>
    /// The carry is the finisher's own knockback, so a record that closes while
    /// its target is still in the air keeps being sampled only while nothing
    /// else moves the target: a Special or Ultimate close stops the sampling at
    /// once, and a swing close voids the carry as soon as the target's HP falls
    /// below its last reading before the close (<see cref="NoteTargetHealth"/>).
    /// A voided record keeps <see cref="Record.CarryPx"/> and
    /// <see cref="Record.LandGapPx"/> at −1 and sets <see cref="Record.CarryVoided"/>.
    /// </para>
    /// </summary>
    public sealed class FinisherReapproachTracker {
        public sealed class Record {
            public int ConnectFrame;
            public ulong TargetId;
            public string TargetKind = "";
            public string Foe = "";
            public bool Air;
            public float TargetStartX;
            /// <summary><c>swing</c> | <c>killed</c> | <c>special</c> | <c>ultimate</c> | <c>death</c> | <c>lost</c>; empty while open.</summary>
            public string Outcome = "";
            /// <summary>Frames from the connect to the closing swing or cast; −1 for the other outcomes.</summary>
            public int Frames = -1;
            public bool HitTaken;
            /// <summary>|target x where it landed − target x at the connect|; −1 until measured.</summary>
            public int CarryPx = -1;
            /// <summary>The hero pivot's horizontal gap to the target hurtbox at that same tick; −1 until measured.</summary>
            public int LandGapPx = -1;
            /// <summary>Index of the closing swing in the telemetry's swing list; −1 unless closed by a swing.</summary>
            public int ClosedBySwingIndex = -1;

            public bool WasAirborne;
            public bool Landed;
            /// <summary>
            /// The record closed before its target landed and something after the
            /// close could have moved the target (a Special or Ultimate cast, or a
            /// hit after a swing close), so the carry was not measured.
            /// </summary>
            public bool CarryVoided;
            /// <summary>The target died or became invalid; its carry is no longer sampled.</summary>
            internal bool TargetGone;
            /// <summary>The target's HP at the latest reading while the record was open; −1 until read.</summary>
            internal int HpBeforeClose = -1;

            public bool IsOpen => Outcome.Length == 0;
        }

        public readonly List<Record> Records = new();

        /// <summary>The open record, or null.</summary>
        public Record Open { get; private set; }

        /// <summary>
        /// The record whose target is still being sampled for its carry: the
        /// open record, or the latest one closed by a swing whose target has not
        /// yet landed (a next swing at another foe can close the record while the
        /// launched target is still in the air), within the give-up window, until
        /// the target is hit again.
        /// </summary>
        public Record Sampled { get; private set; }

        /// <summary>
        /// The <see cref="Sampled"/> target's HP, read at the start of a tick,
        /// before anything that tick can close the record. While the record is
        /// open it is the baseline; once a swing has closed it, a reading below
        /// that baseline means the target was hit after the close, and the carry
        /// is voided rather than booking that hit's knockback as the finisher's.
        /// </summary>
        public void NoteTargetHealth(int hp) {
            Record record = Sampled;
            if (record == null || hp < 0) return;
            if (record.IsOpen) {
                record.HpBeforeClose = hp;
                return;
            }
            if (record.HpBeforeClose >= 0 && hp < record.HpBeforeClose) VoidCarry(record);
        }

        public void OnFinisherConnect(int frame, ulong targetId, string kind, bool air, float targetX, string foe = "") {
            if (Open != null) Close(Open, "swing", frame, -1);
            var record = new Record {
                ConnectFrame = frame,
                TargetId = targetId,
                TargetKind = kind ?? "",
                Foe = foe ?? "",
                Air = air,
                TargetStartX = targetX
            };
            Records.Add(record);
            Open = record;
            Sampled = record;
        }

        /// <summary>The hero's next swing opened (by either path): the open record closes as <c>swing</c>.</summary>
        public void OnSwingStart(int frame, int swingIndex) {
            if (Open == null) return;
            Close(Open, "swing", frame, swingIndex);
        }

        /// <summary>A Special (or the Ultimate) was accepted this tick.</summary>
        public void OnAbilityAccepted(int frame, bool ultimate) {
            if (Open == null) return;
            Close(Open, ultimate ? "ultimate" : "special", frame, -1);
        }

        public void OnHeroHit() {
            if (Open != null) Open.HitTaken = true;
        }

        public void OnHeroDied(int frame) {
            if (Open == null) return;
            Close(Open, "death", frame, -1);
        }

        /// <summary>
        /// One sample of <see cref="Sampled"/>'s target. A dead or invalid
        /// target closes the open record as <c>killed</c>. Otherwise the carry
        /// is measured at the first grounded tick after the target was airborne,
        /// or at <see cref="PlaytestCombatMath.LandingWindowFrames"/> if it never
        /// left the ground.
        /// </summary>
        public void OnTargetSample(int frame, bool alive, bool grounded, float x, float landGapPx) {
            Record record = Sampled;
            if (record == null) return;
            if (!alive) {
                record.TargetGone = true;
                if (record == Open) Close(record, "killed", frame, -1);
                Sampled = null;
                return;
            }
            if (record.Landed) {
                if (record != Open) Sampled = null;
                return;
            }
            if (!grounded) {
                record.WasAirborne = true;
                return;
            }
            bool landedNow = record.WasAirborne
                || frame - record.ConnectFrame >= PlaytestCombatMath.LandingWindowFrames;
            if (!landedNow) return;
            record.Landed = true;
            record.CarryPx = (int)MathF.Round(MathF.Abs(x - record.TargetStartX));
            record.LandGapPx = (int)MathF.Round(MathF.Max(0f, landGapPx));
            if (record != Open) Sampled = null;
        }

        /// <summary>Give-up: an open record past <see cref="PlaytestCombatMath.ReapproachGiveUpFrames"/> closes as <c>lost</c>.</summary>
        public void Tick(int frame) {
            if (Open != null && frame - Open.ConnectFrame >= PlaytestCombatMath.ReapproachGiveUpFrames) {
                Close(Open, "lost", frame, -1);
            }
            if (Sampled != null && Sampled != Open
                && frame - Sampled.ConnectFrame >= PlaytestCombatMath.ReapproachGiveUpFrames) {
                Sampled = null;
            }
        }

        /// <summary>A player rebuild or the end of the run: the open record closes as <c>lost</c>.</summary>
        public void AbandonOpen(int frame) {
            if (Open != null) Close(Open, "lost", frame, -1);
            Sampled = null;
        }

        private void Close(Record record, string outcome, int frame, int swingIndex) {
            record.Outcome = outcome;
            record.Frames = outcome is "swing" or "special" or "ultimate" ? frame - record.ConnectFrame : -1;
            record.ClosedBySwingIndex = outcome == "swing" ? swingIndex : -1;
            if (record == Open) Open = null;
            if (Sampled != record) return;
            if (record.Landed || record.TargetGone || outcome is "death" or "lost") {
                Sampled = null;
                return;
            }
            // Still in the air. A cast may push, pull or hit the target without a
            // readable HP change (a windbox, a vortex), and a swing close with no
            // HP baseline cannot tell a later hit from none: stop measuring.
            if (outcome is "special" or "ultimate" || record.HpBeforeClose < 0) VoidCarry(record);
        }

        private void VoidCarry(Record record) {
            record.CarryVoided = true;
            if (Sampled == record) Sampled = null;
        }

        /// <summary>Frames of the <c>swing</c> outcomes (the re-approach sample).</summary>
        public List<int> SwingFrames() {
            var frames = new List<int>();
            foreach (Record record in Records) {
                if (record.Outcome == "swing") frames.Add(record.Frames);
            }
            return frames;
        }
    }

    /// <summary>
    /// CM-BOT (decision 7.2, 2026-10-16; dossier 2.7.2): the measurement that
    /// decides whether Story needs post-hit mercy invulnerability. Every
    /// positive HP loss the hero takes is one entry: its telemetry frame, a
    /// source key (one key per mob, boss, shooter, Mirror clone or hazard), a
    /// kind and its damage. For each hit H (an entry that is not a periodic tick) at frame F:
    /// <list type="bullet">
    /// <item><b>cross-source re-hit</b> (<see cref="CrossSource30"/>): some
    /// earlier hit from a <i>different</i> source lies in
    /// [F − 30, F);</item>
    /// <item><b>same-source re-hit</b> (<see cref="SameSource30"/>, the
    /// contrast): some earlier hit from the <i>same</i> source lies
    /// in [F − 30, F). A hit can be both;</item>
    /// <item>the 20-frame figures (<see cref="CrossSource20"/>,
    /// <see cref="SameSource20"/>) apply the same tests with a 20-frame window;</item>
    /// <item><see cref="CrossSourceSameFrame"/>: a different-source entry
    /// recorded earlier on the same frame F, which the half-open window above
    /// leaves out (two mobs connecting together; a mercy window opened by the
    /// first would refuse the second).</item>
    /// </list>
    /// <b>Periodic ticks are not hits</b> (CM-FIXA-PRES, 2026-10-16). A
    /// damaging status tick (<see cref="KindStatus"/>, Venom), a zone pulse
    /// (<c>HitDelivery.Tick</c>, the Mirror clone's Rift or Vortex) and a
    /// periodic environmental tick (a drown tick, a stage-hazard DPS payout)
    /// are booked with <c>tick: true</c>. They are kept out of every re-hit
    /// figure, as re-hit and as predecessor, and out of <see cref="Hits"/> and
    /// <see cref="ByKind"/>; they are counted apart in <see cref="Ticks"/>,
    /// <see cref="TickDamage"/> and <see cref="TicksByKind"/> (status ticks
    /// also in <see cref="StatusTicks"/>). A discrete hazard contact (a
    /// discharge, a searchlight strike, an escape catch, debris, a rift snap,
    /// a cyclic-hazard hit) stays a hit and a predecessor.
    /// <para>The <b>combat-only figures</b> (<see cref="CrossSource30Combat"/>,
    /// <see cref="CrossSource20Combat"/>, <see cref="CrossSourceDamage30Combat"/>)
    /// apply the cross-source test with <see cref="KindHazard"/> entries left
    /// out on both sides, so a hazard contact is neither the re-hit nor its
    /// predecessor there: the mob, boss and Mirror re-hits alone.
    /// <see cref="ByKind"/> books by the <i>second</i> hit's kind, so it cannot
    /// remove a hazard predecessor; these figures do.</para>
    /// The log keeps the first <see cref="LogCap"/> hits (ticks are not
    /// logged). Pure: it reads nothing but its arguments.
    /// </summary>
    public sealed class RehitTracker {
        /// <summary>The headline window: a hit within this many frames after another source's hit is a cross-source re-hit.</summary>
        public const int WindowFrames = 30;
        /// <summary>The shorter window the dossier also asks for.</summary>
        public const int ShortWindowFrames = 20;
        /// <summary>At most this many entries are kept in <see cref="Log"/>.</summary>
        public const int LogCap = 500;

        /// <summary>A standard mob's melee or contact hit.</summary>
        public const string KindStandard = "standard";
        /// <summary>An elite mob's melee or contact hit.</summary>
        public const string KindElite = "elite";
        /// <summary>Any hit a boss owns: its melee, its shots and its summons' payloads that name it.</summary>
        public const string KindBoss = "boss";
        /// <summary>Any hit the Level 13 Mirror clone owns, its shots included.</summary>
        public const string KindMirror = "mirror";
        /// <summary>A mob's (standard or elite) shot.</summary>
        public const string KindProjectile = "projectile";
        /// <summary>The environmental chokepoint: hazards, discharges, escape catches, debris.</summary>
        public const string KindHazard = "hazard";
        /// <summary>A damaging status tick (Venom). Always a periodic tick: never part of a re-hit figure.</summary>
        public const string KindStatus = "status";
        /// <summary>A hit whose payload names no owner the telemetry can classify.</summary>
        public const string KindOther = "other";

        /// <summary>One damaging entry, with its re-hit verdicts.</summary>
        public sealed class Entry {
            public int Frame;
            public string Source = "";
            /// <summary>A readable name for the source (enemy or boss ID, hazard ID).</summary>
            public string Label = "";
            public string Kind = "";
            public int Damage;
            /// <summary>A periodic tick (status, zone or environmental): no re-hit verdict is ever set on it.</summary>
            public bool Tick;
            public bool Cross30;
            public bool Cross20;
            /// <summary><see cref="Cross30"/> with hazard entries left out on both sides (the combat-only figure).</summary>
            public bool Cross30Combat;
            /// <summary><see cref="Cross20"/> with hazard entries left out on both sides.</summary>
            public bool Cross20Combat;
            public bool Same30;
            public bool Same20;
            public bool CrossSameFrame;
            /// <summary>Frames back to the latest different-source entry inside the window; −1 for none.</summary>
            public int CrossGap = -1;
            /// <summary>That entry's kind; empty for none.</summary>
            public string CrossKind = "";
        }

        /// <summary>Per-kind totals; a kind's figures count its entries as the re-hit (the second hit).</summary>
        public sealed class KindCounts {
            public int Hits;
            public int Damage;
            public int Cross30;
            public int Cross20;
            public int Same30;
            public int Same20;
            public int CrossDamage30;
            /// <summary>This kind's combat-only cross-source re-hits (30 frames); always 0 for <see cref="KindHazard"/>.</summary>
            public int Cross30Combat;
        }

        /// <summary>Per-kind periodic ticks (CM-FIXA-PRES): counted apart from the hits.</summary>
        public sealed class TickCounts {
            public int Ticks;
            public int Damage;
        }

        /// <summary>Hits: every entry that is not a periodic tick.</summary>
        public int Hits { get; private set; }
        public int HitDamage { get; private set; }
        /// <summary>Hits whose kind is not <see cref="KindHazard"/>: the combat figures' denominator.</summary>
        public int CombatHits { get; private set; }
        /// <summary>Every periodic tick (status, zone and environmental), never a hit.</summary>
        public int Ticks { get; private set; }
        public int TickDamage { get; private set; }
        /// <summary>The status (Venom) subset of <see cref="Ticks"/>.</summary>
        public int StatusTicks { get; private set; }
        public int CrossSource30 { get; private set; }
        public int CrossSource20 { get; private set; }
        /// <summary>Cross-source re-hits (30 frames) with hazard entries left out on both sides.</summary>
        public int CrossSource30Combat { get; private set; }
        /// <summary>Cross-source re-hits (20 frames) with hazard entries left out on both sides.</summary>
        public int CrossSource20Combat { get; private set; }
        /// <summary>Damage dealt by the combat-only cross-source re-hits (30 frames).</summary>
        public int CrossSourceDamage30Combat { get; private set; }
        public int SameSource30 { get; private set; }
        public int SameSource20 { get; private set; }
        public int CrossSourceSameFrame { get; private set; }
        /// <summary>Damage dealt by the cross-source re-hits (30 frames): what a 30-frame mercy window could have refused.</summary>
        public int CrossSourceDamage30 { get; private set; }
        /// <summary>Per gap (1..30 frames) to the latest different-source entry, the cross-source re-hits.</summary>
        public readonly SortedDictionary<int, int> CrossGapHistogram = new();
        /// <summary>Hits only (the headline partition); periodic ticks are in <see cref="TicksByKind"/>.</summary>
        public readonly SortedDictionary<string, KindCounts> ByKind = new(StringComparer.Ordinal);
        /// <summary>Periodic ticks per kind (CM-FIXA-PRES): <c>status</c>, a zone tick's owner kind, <c>hazard</c>.</summary>
        public readonly SortedDictionary<string, TickCounts> TicksByKind = new(StringComparer.Ordinal);
        public readonly List<Entry> Log = new();

        /// <summary>Hits (never ticks) inside the window, oldest first.</summary>
        private readonly List<Entry> _recent = new();

        /// <summary>
        /// Books one damaging entry. Frames must not decrease between calls;
        /// entries on the same frame are taken in call order. Zero or negative
        /// damage books nothing. <paramref name="tick"/> marks a periodic tick
        /// (a <c>HitDelivery.Tick</c> observation); a <see cref="KindStatus"/>
        /// entry is always one. A tick is counted apart and is never a re-hit or
        /// a predecessor.
        /// </summary>
        public Entry Record(int frame, string source, string kind, int damage, string label = "", bool tick = false) {
            if (damage <= 0) return null;
            var entry = new Entry {
                Frame = frame,
                Source = source ?? "",
                Label = label ?? "",
                Kind = string.IsNullOrEmpty(kind) ? KindOther : kind,
                Damage = damage
            };
            entry.Tick = tick || entry.Kind == KindStatus;
            if (entry.Tick) {
                Ticks++;
                TickDamage += damage;
                if (entry.Kind == KindStatus) StatusTicks++;
                TickCounts ticks = TickCountsFor(entry.Kind);
                ticks.Ticks++;
                ticks.Damage += damage;
                return entry;
            }
            KindCounts counts = CountsFor(entry.Kind);
            counts.Hits++;
            counts.Damage += damage;

            bool combatEntry = entry.Kind != KindHazard;
            _recent.RemoveAll(e => e.Frame < frame - WindowFrames);
            foreach (Entry earlier in _recent) {
                bool sameSource = earlier.Source == entry.Source;
                int gap = frame - earlier.Frame;
                if (gap <= 0) {
                    if (!sameSource) entry.CrossSameFrame = true;
                    continue;
                }
                if (sameSource) {
                    entry.Same30 = true;
                    if (gap <= ShortWindowFrames) entry.Same20 = true;
                } else {
                    entry.Cross30 = true;
                    if (gap <= ShortWindowFrames) entry.Cross20 = true;
                    // _recent is oldest first, so the last match is the latest.
                    entry.CrossGap = gap;
                    entry.CrossKind = earlier.Kind;
                    // The combat-only figure: a hazard contact is neither the
                    // re-hit nor its predecessor.
                    if (combatEntry && earlier.Kind != KindHazard) {
                        entry.Cross30Combat = true;
                        if (gap <= ShortWindowFrames) entry.Cross20Combat = true;
                    }
                }
            }

            Hits++;
            HitDamage += damage;
            if (combatEntry) CombatHits++;
            if (entry.Cross30Combat) {
                CrossSource30Combat++;
                CrossSourceDamage30Combat += damage;
                counts.Cross30Combat++;
            }
            if (entry.Cross20Combat) CrossSource20Combat++;
            if (entry.Cross30) {
                CrossSource30++;
                CrossSourceDamage30 += damage;
                counts.Cross30++;
                counts.CrossDamage30 += damage;
                CrossGapHistogram[entry.CrossGap] = CrossGapHistogram.TryGetValue(entry.CrossGap, out int n) ? n + 1 : 1;
            }
            if (entry.Cross20) {
                CrossSource20++;
                counts.Cross20++;
            }
            if (entry.Same30) {
                SameSource30++;
                counts.Same30++;
            }
            if (entry.Same20) {
                SameSource20++;
                counts.Same20++;
            }
            if (entry.CrossSameFrame) CrossSourceSameFrame++;
            _recent.Add(entry);
            if (Log.Count < LogCap) Log.Add(entry);
            return entry;
        }

        private KindCounts CountsFor(string kind) {
            if (!ByKind.TryGetValue(kind, out KindCounts counts)) {
                counts = new KindCounts();
                ByKind[kind] = counts;
            }
            return counts;
        }

        private TickCounts TickCountsFor(string kind) {
            if (!TicksByKind.TryGetValue(kind, out TickCounts counts)) {
                counts = new TickCounts();
                TicksByKind[kind] = counts;
            }
            return counts;
        }
    }

    /// <summary>
    /// The hero's Ultimate meter over a run: level start, gained and spent,
    /// time at full and clean bar-fill times. "Full" is
    /// <see cref="InfluenceMeterRules.MaxValue"/>. A rebuilt hero (an Anchor
    /// Snap) is rebaselined without booking the jump.
    /// </summary>
    public sealed class MeterTracker {
        /// <summary>Meter on the first tracked tick; −1 until then.</summary>
        public float LevelStart = -1f;
        /// <summary>Meter on the latest tracked tick; −1 until then.</summary>
        public float End = -1f;
        public float Gained;
        public float Spent;
        public int FirstFullFrame = -1;
        public int FullFrames;
        /// <summary>Frames from the latest meter ≤ <see cref="PlaytestCombatMath.FillWindowFloor"/> to a clean rising edge to full.</summary>
        public readonly List<int> FillFrames = new();
        /// <summary>Rising edges to full whose window saw a decrease (a Siphon, the Extractor) or began unknown.</summary>
        public int FillsDirty;
        public int TrackedFrames;

        private bool _started;
        private float _last;
        private bool _wasFull;
        private int _windowStart = -1;
        private bool _dirty = true;

        private static float Full => InfluenceMeterRules.MaxValue;

        public void Rebaseline(int frame, float meter) {
            if (!_started) {
                _started = true;
                LevelStart = meter;
            }
            _last = meter;
            _wasFull = meter >= Full;
            End = meter;
            if (meter <= PlaytestCombatMath.FillWindowFloor) {
                _windowStart = frame;
                _dirty = false;
            } else {
                // A rebuilt hero carrying a durable meter: its fill began unseen.
                _dirty = true;
            }
        }

        public void Sample(int frame, float meter) {
            if (!_started) Rebaseline(frame, meter);
            TrackedFrames++;
            float delta = meter - _last;
            if (delta > 0f) Gained += delta;
            else if (delta < 0f) {
                Spent -= delta;
                _dirty = true;
            }
            if (meter <= PlaytestCombatMath.FillWindowFloor) {
                _windowStart = frame;
                _dirty = false;
            }
            bool full = meter >= Full;
            if (full) {
                FullFrames++;
                if (FirstFullFrame < 0) FirstFullFrame = frame;
                if (!_wasFull) {
                    if (!_dirty && _windowStart >= 0) FillFrames.Add(frame - _windowStart);
                    else FillsDirty++;
                }
            }
            _wasFull = full;
            _last = meter;
            End = meter;
        }
    }

    /// <summary>
    /// The active Defy over a run (meter and Defy update 2026-10-31, plan 2.13, O24;
    /// ledger <c>PROVISIONAL-BOT-DEFY-POLICY</c>), replacing the passive-proc count the old
    /// <c>StoryDefyHistoryUsed</c> latch fed. Each execution (the blast frame) is booked with the
    /// meter before the 50 was spent, the targets blasted and the HP healed; the hero's discrete
    /// hits and deaths are kept as frames so each execution can be resolved against the
    /// <see cref="WindowFrames"/> before and after it, and <c>died_within_3s</c> (the user's
    /// original complaint) read off the deaths. Periodic ticks are not hits.
    /// </summary>
    public sealed class DefyTracker {
        /// <summary>The before and after window: three seconds.</summary>
        public const int WindowFrames = 180;

        /// <summary>One executed Defy, as the blast reported it.</summary>
        public sealed class Execution {
            public int Frame;
            public float MeterBefore;
            public int Targets;
            public int Healed;
        }

        /// <summary>An execution resolved against the hits and deaths around it.</summary>
        public sealed class Resolved {
            public Execution Execution;
            public int HitsBefore;
            public int HitsAfter;
            public bool DiedWithin3s;
            /// <summary>The run ended inside the after window, so <see cref="HitsAfter"/> and the death flag may be short.</summary>
            public bool AfterTruncated;
        }

        private readonly List<Execution> _executions = new();
        private readonly List<int> _hits = new();
        private readonly List<int> _deaths = new();

        /// <summary>Defy presses the bot made.</summary>
        public int Pressed;
        /// <summary>Presses the hero was not able to take, by the label of the first reason.</summary>
        public readonly Dictionary<string, int> RefusedBy = new();

        public IReadOnlyList<Execution> Executions => _executions;
        public int Executed => _executions.Count;

        public void RecordHit(int frame) => _hits.Add(frame);
        public void RecordDeath(int frame) => _deaths.Add(frame);

        public void RecordExecution(int frame, float meterBefore, int targets, int healed) =>
            _executions.Add(new Execution { Frame = frame, MeterBefore = meterBefore, Targets = targets, Healed = healed });

        /// <summary>A press: books it, and its refusal label when the hero could not take it.</summary>
        public void RecordPress(bool canDefy, float meter) {
            Pressed++;
            if (canDefy) return;
            string label = meter < BasicComboRules.DefyInfluenceCost ? "meter" : "state";
            RefusedBy[label] = RefusedBy.GetValueOrDefault(label) + 1;
        }

        /// <summary>Every execution with the hits taken in the 180 frames before and after and whether the hero died within them.</summary>
        public List<Resolved> Resolve(int endFrame) {
            var list = new List<Resolved>(_executions.Count);
            foreach (Execution e in _executions) {
                int before = 0, after = 0;
                foreach (int h in _hits) {
                    if (h >= e.Frame - WindowFrames && h < e.Frame) before++;
                    else if (h > e.Frame && h <= e.Frame + WindowFrames) after++;
                }
                bool died = false;
                foreach (int d in _deaths) if (d > e.Frame && d <= e.Frame + WindowFrames) died = true;
                list.Add(new Resolved {
                    Execution = e, HitsBefore = before, HitsAfter = after, DiedWithin3s = died,
                    AfterTruncated = endFrame < e.Frame + WindowFrames
                });
            }
            return list;
        }
    }

    /// <summary>
    /// The Momentum gauge over a run (meter and Defy update 2026-10-31, plan 2.13): where it
    /// started (2.0 at a fresh level, the analogue of <c>meter0</c>), what it gained and from
    /// where, what the Specials spent, and how long it sat starved (under one whole pip) or full.
    /// The hero raises no gain event, so a tick's rise is split from the readable pieces: up to
    /// one tick of regen is booked as <c>regen</c>, and anything above it to the hit the telemetry
    /// heard land that tick (the swing's hitbox ID; <c>other</c> without one). A tick that carries
    /// both a regen and a hit gain therefore mis-books at most 1/720 pip, and a gain lost to the cap
    /// is not seen. Spends are the Specials' execution serials, not a negative step.
    /// </summary>
    public sealed class MomentumTracker {
        public const string RegenSource = "regen";
        public const string OtherSource = "other";
        private const float Epsilon = 0.0001f;

        /// <summary>Pips on the first tracked tick; -1 until then.</summary>
        public float Start = -1f;
        public float End = -1f;
        public readonly Dictionary<string, double> GainedBy = new();
        public int SpentSpecial1;
        public int SpentSpecial2;
        public int TrackedFrames;
        /// <summary>Ticks with under one whole pip (a Special cannot be paid).</summary>
        public int FramesStarved;
        /// <summary>Ticks with the gauge full.</summary>
        public int FramesFull;

        private bool _started;
        private float _last;

        public int SpecialsCast => SpentSpecial1 + SpentSpecial2;
        public double Gained { get { double sum = 0; foreach (double v in GainedBy.Values) sum += v; return sum; } }

        /// <summary>Sets the baseline without booking the jump (the first tick, or a rebuilt hero).</summary>
        public void Rebaseline(float pips) {
            if (!_started) {
                _started = true;
                Start = pips;
            }
            _last = pips;
            End = pips;
        }

        /// <param name="pips">The gauge now.</param>
        /// <param name="regenPerTick">One tick of regen at the hero's multiplier.</param>
        /// <param name="cast1">Special 1 was accepted this tick.</param>
        /// <param name="cast2">Special 2 was accepted this tick.</param>
        /// <param name="hitSource">The hitbox ID of a swing that first landed this tick, or null.</param>
        public void Sample(float pips, float regenPerTick, bool cast1, bool cast2, string hitSource) {
            if (!_started) Rebaseline(pips);
            TrackedFrames++;
            if (cast1) SpentSpecial1++;
            if (cast2) SpentSpecial2++;
            float spent = (cast1 ? PlaytestCombatMath.SpecialCostPips : 0f) + (cast2 ? PlaytestCombatMath.SpecialCostPips : 0f);
            float gross = pips - _last + spent;
            if (gross > Epsilon) {
                float regen = Math.Min(gross, regenPerTick);
                Add(RegenSource, regen);
                float excess = gross - regen;
                if (excess > Epsilon) Add(string.IsNullOrEmpty(hitSource) ? OtherSource : hitSource, excess);
            }
            if (pips < PlaytestCombatMath.SpecialCostPips - PlaytestCombatMath.PipSlack) FramesStarved++;
            if (pips >= BasicComboRules.MomentumMaxPips - Epsilon) FramesFull++;
            _last = pips;
            End = pips;
        }

        private void Add(string source, float amount) =>
            GainedBy[source] = GainedBy.GetValueOrDefault(source) + amount;
    }
}
