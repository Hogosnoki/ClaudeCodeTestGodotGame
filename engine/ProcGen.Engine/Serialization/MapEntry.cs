// File-scoped so this compiles cleanly even when copied into a host project that hasn't
// opted into <Nullable>enable</Nullable> project-wide.
#nullable enable
using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;

namespace ProcGen.Engine.Serialization
{
    /// <summary>
    /// One map's configuration plus the override diff painted on top of it -- one element of a
    /// <see cref="ProjectFile"/>. <see cref="Map"/> and <see cref="Overrides"/> are left
    /// un-defaulted (null when absent from the JSON) rather than defaulted to empty, so the loader
    /// can tell "entry has no map at all" apart from "entry has an intentionally empty map".
    /// </summary>
    public sealed class MapEntry
    {
        public MapDefinition? Map { get; set; }
        public List<TileOverride>? Overrides { get; set; }
    }
}
