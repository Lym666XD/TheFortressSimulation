using System;
using System.Runtime.CompilerServices;

namespace HumanFortress.Core.Content;

/// <summary>
/// Fixed-point math utilities for performance-critical material calculations.
/// Per MATERIALS_SPEC v4-min: FX = 10_000 (1.0000)
/// All dimensionless values (multipliers, resist multipliers, 0..100 scales) use FX integers.
/// Only density remains in physical units (kg/m³ == mg/mL numerically).
/// </summary>
public static class FixedPoint
{
    /// <summary>
    /// Fixed-point scale: 1.0000 = 10000
    /// </summary>
    public const int FX = 10000;

    /// <summary>
    /// Half of FX for rounding and centering calculations
    /// </summary>
    public const int FxHalf = FX / 2;

    /// <summary>
    /// Returns FX representation of 1.0
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int One() => FX;

    /// <summary>
    /// Convert float to FX integer (rounded)
    /// Example: 1.5 → 15000, 0.25 → 2500
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FromFloat(double x) => (int)Math.Round(x * FX);

    /// <summary>
    /// Convert percentage (0..100) to FX integer
    /// Example: 50 → 5000 (0.5), 100 → 10000 (1.0)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FromPct(double p) => (int)Math.Round((p / 100.0) * FX);

    /// <summary>
    /// Convert FX integer back to float (for display/debugging)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToFloat(int fx) => (double)fx / FX;

    /// <summary>
    /// Convert FX integer back to percentage
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToPct(int fx) => ((double)fx / FX) * 100.0;

    /// <summary>
    /// Multiply two FX integers (rounded)
    /// Example: fx_mul(5000, 15000) = 0.5 * 1.5 = 0.75 = 7500
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mul(int a, int b)
    {
        return (int)(((long)a * b + FxHalf) / FX);
    }

    /// <summary>
    /// Divide two FX integers (rounded)
    /// Example: fx_div(5000, 20000) = 0.5 / 2.0 = 0.25 = 2500
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Div(int a, int b)
    {
        if (b == 0) return 0;
        return (int)(((long)a * FX + b / 2) / b);
    }

    /// <summary>
    /// Center a 0..FX value around 0: returns [-FX/2, +FX/2]
    /// Used for material signals where 50 is neutral (e.g., hardness-50)
    /// Example: Dev(5000) = 0 (neutral), Dev(7500) = +2500 (above neutral)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Dev(int fx) => fx - FxHalf;

    /// <summary>
    /// Normalize 0..FX to [-FX, +FX] for strong signals (optional)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NormFull(int fx) => (int)(((long)fx - FxHalf) * 2);

    /// <summary>
    /// Clamp value to range [min, max]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Clamp double value to range [min, max]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Integer square root (for power calculations without floats)
    /// </summary>
    public static int ISqrt(int value)
    {
        if (value <= 0) return 0;

        int x = value;
        int y = (x + 1) / 2;

        while (y < x)
        {
            x = y;
            y = (x + value / x) / 2;
        }

        return x;
    }

    /// <summary>
    /// Linear interpolation between two FX integers
    /// t is 0..FX representing 0.0..1.0
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Lerp(int a, int b, int tFx)
    {
        return a + Mul(b - a, tFx);
    }

    /// <summary>
    /// Interpolate from a lookup table (LUT) for expensive functions like pow()
    /// ratioPointsFx and valuePointsFx must be sorted by ratio
    /// </summary>
    public static int InterpolateLUT(int[] ratioPointsFx, int[] valuePointsFx, int ratioFx)
    {
        if (ratioPointsFx.Length == 0) return FX;
        if (ratioPointsFx.Length != valuePointsFx.Length)
            throw new ArgumentException("LUT arrays must have same length");

        // Below first point
        if (ratioFx <= ratioPointsFx[0])
            return valuePointsFx[0];

        // Above last point
        if (ratioFx >= ratioPointsFx[^1])
            return valuePointsFx[^1];

        // Find interval and interpolate
        for (int i = 0; i < ratioPointsFx.Length - 1; i++)
        {
            if (ratioFx >= ratioPointsFx[i] && ratioFx <= ratioPointsFx[i + 1])
            {
                int r0 = ratioPointsFx[i];
                int r1 = ratioPointsFx[i + 1];
                int v0 = valuePointsFx[i];
                int v1 = valuePointsFx[i + 1];

                // Linear interpolation: v = v0 + (ratio - r0) / (r1 - r0) * (v1 - v0)
                int t = Div(ratioFx - r0, r1 - r0);
                return Lerp(v0, v1, t);
            }
        }

        return FX; // fallback
    }
}
