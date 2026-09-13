using Godot;

namespace FTT.Core {

    /// <summary>
    /// The authored audio bus hierarchy, per <c>design-godot.md</c>'s
    /// "Audio Engine Integration" graph:
    ///
    /// <code>
    /// Master
    ///   +-- Music                 (background; carries the selected low-pass)
    ///   +-- SFX                   (the player's SFX gain/mute — no filter of its own)
    ///   |     +-- Combat          \
    ///   |     +-- Movement         &gt;  background World SFX; each carries the filter
    ///   |     +-- Environmental   /
    ///   |     +-- CriticalCues    (the clear path: warnings, freeze/thaw, recovery,
    ///   |                          phase and timer-danger cues, result calls)
    ///   +-- UI                    (menu sounds; outside the background chain)
    ///         +-- Dialogue        (the authored synthetic voice/chirp character)
    /// </code>
    ///
    /// <para><b>Package 11 A8 / C01b.</b> The low-pass moved <em>down</em> from
    /// <see cref="SFX"/> onto the three background children. That is what lets
    /// <see cref="CriticalCues"/> sit under the same user SFX gain and mute — C01b
    /// requires the split to live "below those user gain controls" — while never
    /// inheriting the background muffling, pitch warp or ducking. Leaving the
    /// filter on the shared parent would have muffled the protected cues along
    /// with everything else, which is the exact failure the contract names:
    /// "never a second filter on a parent bus carrying protected cues".</para>
    ///
    /// <para>The layout itself lives in <c>resources/Audio/default_bus_layout.tres</c>
    /// and is registered in <c>project.godot</c> under
    /// <c>audio/buses/default_bus_layout</c>, so the engine builds it before any
    /// autoload runs. <see cref="EnsureBuses"/> is a defensive fallback only — it
    /// recreates a missing bus rather than letting a mis-routed play call land on
    /// Master. Nothing routes to an "Ambient" bus: the old runtime-created one was
    /// orphaned and is gone (the ambient *stem* is music, and plays on
    /// <see cref="Music"/>).</para>
    /// </summary>
    public static class AudioBuses {
        public const string Master = "Master";
        public const string Music = "Music";
        public const string SFX = "SFX";
        public const string UI = "UI";
        public const string Combat = "Combat";
        public const string Movement = "Movement";
        public const string Environmental = "Environmental";

        /// <summary>
        /// C01b's clear path. Attack-class and hazard warnings, impending-platform
        /// danger, freeze/thaw, recovery/phase cues, timer-danger cues and
        /// confirmed result announcements play here and bypass every background
        /// low-pass, pitch bend, pan and duck — while still respecting Master and
        /// the player's SFX gain/mute, because it still sends into
        /// <see cref="SFX"/>.
        /// </summary>
        public const string CriticalCues = "CriticalCues";

        /// <summary>
        /// C01b's dialogue path. The authored synthetic voice/chirp character is
        /// preserved and is never additionally warped or muffled by an
        /// environmental or health profile — and never automatically boosted.
        /// </summary>
        public const string Dialogue = "Dialogue";

        /// <summary>Every bus the authored layout must contain, in layout order.</summary>
        public static readonly string[] All = {
            Master, Music, SFX, UI, Combat, Movement, Environmental, CriticalCues, Dialogue
        };

        /// <summary>
        /// The background World SFX buses — the ones a selected profile's low-pass
        /// is written to. <see cref="CriticalCues"/> is deliberately absent.
        /// </summary>
        public static readonly string[] BackgroundSfxBuses = { Combat, Movement, Environmental };

        /// <summary>
        /// Buses a background mix profile may never filter, duck or pitch. The
        /// mixer refuses to write to these and the bus-layout test asserts none of
        /// them carries a filter.
        /// </summary>
        public static readonly string[] ProtectedBuses = { CriticalCues, UI, Dialogue };

        /// <summary>
        /// Bus name to the bus it sends into. Master sends to the output device and
        /// maps to an empty string.
        /// </summary>
        public static string SendTargetOf(string busName) => busName switch {
            Master => "",
            Music or SFX or UI => Master,
            Combat or Movement or Environmental or CriticalCues => SFX,
            Dialogue => UI,
            _ => Master
        };

        /// <summary>
        /// Resolves a bus index, creating the bus if the authored layout is missing
        /// or has been edited out from under us. Returns 0 (Master) when the name is
        /// unknown, which is audible rather than silent — a missing sub-bus should
        /// not swallow a sound effect.
        /// </summary>
        public static int Resolve(string busName) {
            int index = AudioServer.GetBusIndex(busName);
            if (index >= 0) return index;

            AudioServer.AddBus();
            int created = AudioServer.BusCount - 1;
            AudioServer.SetBusName(created, busName);
            string send = SendTargetOf(busName);
            if (!string.IsNullOrEmpty(send) && AudioServer.GetBusIndex(send) >= 0) {
                AudioServer.SetBusSend(created, send);
            }
            return created;
        }

        /// <summary>Creates any bus the authored layout failed to provide.</summary>
        public static void EnsureBuses() {
            foreach (string busName in All) Resolve(busName);
        }

        /// <summary>
        /// True when every authored bus exists and routes to its documented parent.
        /// The bus-layout test asserts this against the shipped resource.
        /// </summary>
        public static bool HierarchyIsIntact() {
            foreach (string busName in All) {
                int index = AudioServer.GetBusIndex(busName);
                if (index < 0) return false;
                if (busName == Master) continue;
                if (AudioServer.GetBusSend(index) != SendTargetOf(busName)) return false;
            }
            return true;
        }
    }
}
