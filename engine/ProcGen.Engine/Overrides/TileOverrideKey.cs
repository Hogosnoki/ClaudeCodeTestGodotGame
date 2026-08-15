using System;

namespace ProcGen.Engine.Overrides
{
    public readonly struct TileOverrideKey : IEquatable<TileOverrideKey>
    {
        public string LayerId { get; }
        public int X { get; }
        public int Y { get; }

        public TileOverrideKey(string layerId, int x, int y)
        {
            LayerId = layerId;
            X = x;
            Y = y;
        }

        public bool Equals(TileOverrideKey other) =>
            X == other.X && Y == other.Y && string.Equals(LayerId, other.LayerId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is TileOverrideKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(LayerId, X, Y);
    }
}
