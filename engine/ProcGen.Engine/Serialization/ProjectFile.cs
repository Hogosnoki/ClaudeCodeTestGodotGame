using System.Collections.Generic;

namespace ProcGen.Engine.Serialization
{
    /// <summary>
    /// The on-disk unit: every map belonging to one game, together in one file. A project *is* a
    /// game -- there is deliberately no way for one project file to reference a map living in
    /// another; an <see cref="Model.ExitPoint"/>'s DestinationMapId only ever needs to resolve
    /// against maps in this same file. That's exactly why map ids only need to be unique within a
    /// project (enforced by <see cref="ProjectFileSerializer"/> on load) rather than globally.
    /// </summary>
    public sealed class ProjectFile
    {
        public int FormatVersion { get; set; } = ProjectFileSerializer.CurrentFormatVersion;
        public List<MapEntry> Maps { get; set; } = new List<MapEntry>();
    }
}
