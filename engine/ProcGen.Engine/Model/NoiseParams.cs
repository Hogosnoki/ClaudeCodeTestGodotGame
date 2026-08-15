namespace ProcGen.Engine.Model
{
    /// <summary>Per-layer fBm noise parameters.</summary>
    public sealed class NoiseParams
    {
        public int Octaves { get; set; } = 1;
        public double Frequency { get; set; } = 1.0;
        public double Persistence { get; set; } = 0.5;
        public double Lacunarity { get; set; } = 2.0;
    }
}
