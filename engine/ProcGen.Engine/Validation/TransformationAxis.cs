using System;

namespace ProcGen.Engine.Validation
{
    /// <summary>
    /// The engine-owned rule for the Transformation axis: it must advance in steps no larger than
    /// 0.1 to preserve shape continuity. Living here (not duplicated in the tool and the game)
    /// guarantees both consumers enforce it identically.
    /// </summary>
    public static class TransformationAxis
    {
        public const double MaxStep = 0.1;

        // Tolerance for floating-point comparison noise around the 0.1 boundary itself.
        private const double Epsilon = 1e-9;

        public static bool IsValidStep(double from, double to) => Math.Abs(to - from) <= MaxStep + Epsilon;

        /// <summary>Clamps `to` so the step from `from` never exceeds MaxStep, preserving direction.</summary>
        public static double ClampStep(double from, double to)
        {
            double delta = to - from;
            if (delta > MaxStep) return from + MaxStep;
            if (delta < -MaxStep) return from - MaxStep;
            return to;
        }
    }
}
