using Godot;

namespace FTT.Core {

    /// <summary>
    /// The authored audio bus hierarchy, per <c>design-godot.md</c>'s
    /// "Audio Engine Integration" graph:
    ///
    /// <code>
    /// Master
    ///   +-- Music
    ///   +-- SFX
    ///   |     +-- Combat
    ///   |     +-- Movement
    ///   |     +-- Environmental
    ///   +-- UI            (dialogue chirps and menu sounds; deliberately outside
    ///                      the SFX filtering chain so prompts stay legible)
    /// </code>
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

        /// <summary>Every bus the authored layout must contain, in layout order.</summary>
        public static readonly string[] All = { Master, Music, SFX, UI, Combat, Movement, Environmental };

        /// <summary>
        /// Bus name to the bus it sends into. Master sends to the output device and
        /// maps to an empty string.
        /// </summary>
        public static string SendTargetOf(string busName) => busName switch {
            Master => "",
            Music or SFX or UI => Master,
            Combat or Movement or Environmental => SFX,
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
