using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Gameplay update 2026-10-12 (GU-E, user request 3, plan §2.3): the base
    /// level's encounter-lock builder and its wiring — the camera lock layer, the
    /// objective while a lock is engaged and its restore afterwards, and the F10
    /// baseline that builds a lock Cleared on a resume past it.
    ///
    /// <para><b>Authoring</b> (the AUTHORING GUIDE in <c>docs/handoffs/GU_E.md</c>
    /// has the whole recipe): build every lock in <see cref="BuildLevel"/> with
    /// <see cref="BuildEncounterLock"/>, list each lock ID in the
    /// <see cref="EncounterBaselineMap"/> of every checkpoint after it, and keep
    /// checkpoints, the Restoration Font and the sealing anchor out of every area.
    /// The base arms the locks itself once the resume baseline is known.</para>
    /// </summary>
    public abstract partial class StoryLevelControllerBase {

        private readonly List<EncounterLock> _encounterLocks = new();

        /// <summary>The locks this level built, in authoring order. Test and bot seam.</summary>
        public IReadOnlyList<EncounterLock> EncounterLocks => _encounterLocks;

        /// <summary>The lock the hero is fighting in right now, or null.</summary>
        public EncounterLock ActiveEncounterLock {
            get {
                foreach (EncounterLock encounterLock in _encounterLocks) {
                    if (IsInstanceValid(encounterLock) && encounterLock.State == EncounterLockState.Engaged) {
                        return encounterLock;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Builds one encounter lock from <paramref name="spec"/> and adds it to the
        /// level, barriers open. Call it from <see cref="BuildLevel"/>: the resume
        /// baseline is applied after <see cref="BuildLevel"/> and before the base
        /// arms the locks, which is what lets a lock in the baseline build Cleared
        /// with no member ever spawned. The spec's level bounds default to
        /// <see cref="LevelBounds"/>.
        /// </summary>
        protected EncounterLock BuildEncounterLock(EncounterLockSpec spec) {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            spec.LevelBounds ??= LevelBounds;
            EncounterLock encounterLock = EncounterLock.Create(spec);
            encounterLock.StateChanged += OnEncounterLockStateChanged;
            AddChild(encounterLock);
            _encounterLocks.Add(encounterLock);
            return encounterLock;
        }

        /// <summary>
        /// Hands every lock the hero and the camera, then arms it: the exit
        /// barrier closes and the pre-placed members spawn dormant — unless the
        /// resume baseline already restored the lock Cleared.
        /// </summary>
        private void ArmEncounterLocks() {
            foreach (EncounterLock encounterLock in _encounterLocks) {
                if (!IsInstanceValid(encounterLock)) continue;
                encounterLock.Hero = Player;
                encounterLock.Camera = Camera;
                encounterLock.Arm();
            }
        }

        /// <summary>
        /// The F10 baseline for a lock: true when <paramref name="encounterID"/>
        /// names one of this level's locks, which is then built Cleared.
        /// </summary>
        private bool RestoreEncounterLockCleared(string encounterID) {
            bool restored = false;
            foreach (EncounterLock encounterLock in _encounterLocks) {
                if (!IsInstanceValid(encounterLock) || encounterLock.LockID != encounterID) continue;
                encounterLock.RestoreCleared();
                restored = true;
            }
            return restored;
        }

        private void OnEncounterLockStateChanged(EncounterLock encounterLock, EncounterLockState previous,
            EncounterLockState current) {
            if (current == EncounterLockState.Engaged) {
                if (!string.IsNullOrWhiteSpace(encounterLock.ObjectiveKey)) HUD?.SetObjective(encounterLock.ObjectiveKey);
                return;
            }
            if (previous == EncounterLockState.Engaged) RestoreLevelObjective();
        }

        // === The objective-restore hook ======================================

        private string _levelObjectiveKey = "";
        private object[] _levelObjectiveArguments = Array.Empty<object>();

        /// <summary>
        /// Re-posts the level's own objective once an engaged lock clears or
        /// reverts (a lock posts its own while engaged). The default re-posts the
        /// last objective the level set through <see cref="SetObjective"/>, else
        /// the initial one; a level whose objective is derived from live state
        /// overrides it to recompute.
        /// </summary>
        protected virtual void RestoreLevelObjective() {
            string key = string.IsNullOrWhiteSpace(_levelObjectiveKey) ? InitialObjectiveKey : _levelObjectiveKey;
            if (string.IsNullOrWhiteSpace(key)) return;
            HUD?.SetObjective(key, _levelObjectiveArguments);
        }

        /// <summary>Remembers the level's objective for <see cref="RestoreLevelObjective"/>.</summary>
        private void RememberLevelObjective(string translationKey, object[] arguments) {
            _levelObjectiveKey = translationKey ?? "";
            _levelObjectiveArguments = arguments ?? Array.Empty<object>();
        }

        // === FIX-LEVEL (2026-10-13): room seams never post over a lock ========

        private int _roomEntryDepth;

        /// <summary>
        /// Runs a room trigger's <c>onEntered</c> (<see cref="BuildRoomTransition"/>).
        /// While it runs, an objective it posts through <see cref="SetObjective"/>
        /// is held back if an engaged lock posts its own
        /// (<see cref="RoomEntryObjectiveDeferredToLock"/>).
        /// </summary>
        private void RunRoomEntry(Action<string> onEntered, string roomID) {
            _roomEntryDepth++;
            try {
                onEntered(roomID);
            } finally {
                _roomEntryDepth--;
            }
        }

        /// <summary>
        /// True while a room trigger's <c>onEntered</c> runs and an Engaged lock
        /// with an <see cref="EncounterLock.ObjectiveKey"/> holds the objective
        /// line. The room's objective is only remembered then, and the lock's
        /// release hands it back (<see cref="RestoreLevelObjective"/>). Room seams
        /// sit on or inside lock areas (Level 7's boarding channel trigger stands
        /// 200 px into the boarding fight; the repeatable forward triggers of
        /// Levels 12–15 sit just inside an area's entry and fire again whenever
        /// the hero steps back over them), and each crossing used to replace
        /// "Defeat every enemy here" for the rest of the fight. Only room entries
        /// are held: a level's own mid-fight warnings (a searchlight alarm, an
        /// exposure) still post, as their levels intend.
        /// </summary>
        private bool RoomEntryObjectiveDeferredToLock =>
            _roomEntryDepth > 0 && ActiveEncounterLock is EncounterLock held
            && !string.IsNullOrWhiteSpace(held.ObjectiveKey);
    }
}
