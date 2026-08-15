using System.Text.Json.Serialization;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// A layer's independent position in the 3D generation space: spatial X/Y plus the
    /// Transformation axis. This is the layer's "seed" -- not a single integer, but a full
    /// offset into the shared lattice, which is what decorrelates layers from one another.
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
