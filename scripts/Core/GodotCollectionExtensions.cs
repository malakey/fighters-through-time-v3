using Godot;

namespace FTT.Core {

    /// <summary>
    /// Engine calls such as GetNodesInGroup, GetChildren, and
    /// GetOverlappingBodies return Godot collection wrappers whose native
    /// storage is otherwise reclaimed by a finalizer. A collection finalizer
    /// that runs during engine teardown faults inside godotsharp_array_destroy
    /// (0xC0000005), so recurring gameplay call sites dispose these wrappers
    /// deterministically instead of leaving them to the GC. GameManager
    /// drains whatever still reaches the finalizer queue before quit.
    /// </summary>
    public static class GodotCollectionExtensions {
        /// <summary>
        /// Returns the disposable non-generic view of a typed Godot array for
        /// a using statement. Both views share one native array; disposing
        /// either frees it immediately.
        /// </summary>
        public static Godot.Collections.Array AsDisposable<[MustBeVariant] T>(
            this Godot.Collections.Array<T> array) => (Godot.Collections.Array)array;
    }
}
