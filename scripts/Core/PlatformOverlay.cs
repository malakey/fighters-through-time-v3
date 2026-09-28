using System;

namespace FTT.Core {

    /// <summary>
    /// Package 12 W6 (G11, adopted D7(a)). The seam a platform overlay — the Steam
    /// overlay — reports through. D7(a) keeps Steamworks out of this package, so
    /// the only implementation that ships is <see cref="NoOpPlatformOverlay"/>,
    /// and <b>nothing real ever invokes it</b>. When Steam integration lands, its
    /// overlay callback calls <see cref="PlatformOverlay.ReportOverlayActive"/>
    /// and every pausable surface opens its ordinary pause menu, exactly as it
    /// already does on window focus loss (<c>PauseMenuBase.HandleFocusLost</c>).
    /// </summary>
    public interface IPlatformOverlay {
        /// <summary>A stable name for logs ("none", "steam").</summary>
        string Name { get; }

        /// <summary>True when the platform can show an in-game overlay at all.</summary>
        bool IsAvailable { get; }
    }

    /// <summary>The shipped implementation: no platform, no overlay, no callbacks.</summary>
    public sealed class NoOpPlatformOverlay : IPlatformOverlay {
        public string Name => "none";
        public bool IsAvailable => false;
    }

    /// <summary>The single registration point for the platform overlay hook.</summary>
    public static class PlatformOverlay {
        /// <summary>The installed implementation. <see cref="NoOpPlatformOverlay"/> unless a platform layer replaces it.</summary>
        public static IPlatformOverlay Current { get; private set; } = new NoOpPlatformOverlay();

        /// <summary>Raised with <c>true</c> when the overlay opens and <c>false</c> when it closes.</summary>
        public static event Action<bool> OverlayActivated;

        /// <summary>Installs a platform implementation (null restores the no-op).</summary>
        public static void Install(IPlatformOverlay overlay) => Current = overlay ?? new NoOpPlatformOverlay();

        /// <summary>
        /// The one entry point a platform callback uses. Invoked nowhere in this
        /// build (D7(a)); tests drive it to prove the pause wiring.
        /// </summary>
        public static void ReportOverlayActive(bool active) => OverlayActivated?.Invoke(active);
    }
}
