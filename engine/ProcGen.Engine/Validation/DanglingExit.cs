namespace ProcGen.Engine.Validation
{
    public enum DanglingExitReason
    {
        /// <summary>DestinationMapId doesn't match any map in the project.</summary>
        MapNotFound,

        /// <summary>The destination map exists, but has no entrance with DestinationEntranceId.</summary>
        EntranceNotFound,
    }

    /// <summary>One exit whose destination doesn't resolve within its own project. See <see cref="ProjectValidation.FindDanglingExits"/>.</summary>
    public readonly struct DanglingExit
    {
        public string SourceMapId { get; }
        public string ExitId { get; }
        public string DestinationMapId { get; }
        public string DestinationEntranceId { get; }
        public DanglingExitReason Reason { get; }

        public DanglingExit(string sourceMapId, string exitId, string destinationMapId, string destinationEntranceId, DanglingExitReason reason)
        {
            SourceMapId = sourceMapId;
            ExitId = exitId;
            DestinationMapId = destinationMapId;
            DestinationEntranceId = destinationEntranceId;
            Reason = reason;
        }
    }
}
