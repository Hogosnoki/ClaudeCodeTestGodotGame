// File-scoped so this compiles cleanly even when copied into a host project that hasn't
// opted into <Nullable>enable</Nullable> project-wide.
#nullable enable
using System.Collections.Generic;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;

namespace ProcGen.Engine.Serialization
{
    /// <summary>
    /// The on-disk unit: the full map configuration plus the override diff painted on top of it --
    /// together, exactly the "configuration" half of "configuration + algorithm reproduces the map
    /// exactly". Nothing editor-only (e.g. the tool's tile display colors) belongs here; see
    /// <see cref="MapFileSerializer"/>'s doc comment for why.
    /// <see cref="Map"/> and <see cref="Overrides"/> are left un-defaulted (null when absent from
    /// the JSON) rather than defaulted to empty, so the loader can tell "file has no map section
    /// at all" apart from "file has an intentionally empty map".
    /// </summary>
    public sealed class MapFile
    {
        public int FormatVersion { get; set; } = MapFileSerializer.CurrentFormatVersion;
        public MapDefinition? Map { get; set; }
        public List<TileOverride>? Overrides { get; set; }
    }
}
