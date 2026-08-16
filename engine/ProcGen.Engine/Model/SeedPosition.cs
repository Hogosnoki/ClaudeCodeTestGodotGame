using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A layer's independent position in the 3D generation space: spatial X/Y plus the
    /// Transformation axis. This is the layer's "seed" -- not a single integer, but a full
    /// offset into the shared lattice, which is what decorrelates layers from one another.
    /// Applied by <see cref="ProcGen.Engine.Noise.LatticeNoise3D.SampleFbm"/> as a fixed
    /// lattice-space translation added AFTER frequency scaling -- it stays put regardless of
    /// whatever frequency/lacunarity a layer is tuned to, rather than being re-multiplied by
    /// them every time those change.
    /// </summary>
    public readonly struct SeedPosition
    {
        public double X { get; }
        public double Y { get; }
        public double T { get; }

        [JsonConstructor]
        public SeedPosition(double x, double y, double t)
        {
            X = x;
            Y = y;
            T = t;
        }
    }
}
