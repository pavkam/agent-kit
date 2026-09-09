// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;
using System.Numerics;

/// <summary>Represents one canonical, nonnegative base-ten budget quantity without decimal magnitude loss.</summary>
/// <remarks>The value is <see cref="Coefficient"/> times ten to the negative <see cref="Scale"/>. Construction removes trailing decimal zeroes, so equality and hashing are exact and canonical. The default value is the valid exact zero.</remarks>
public readonly record struct BudgetQuantity: IComparable<BudgetQuantity>
{
    /// <summary>Initializes a canonical exact quantity.</summary>
    /// <param name="coefficient">The nonnegative unscaled quantity.</param>
    /// <param name="scale">The number of fractional base-ten digits, from zero through 28.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="coefficient"/> is negative or <paramref name="scale"/> is outside zero through 28.</exception>
    public BudgetQuantity(BigInteger coefficient, int scale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, 28);
        ArgumentOutOfRangeException.ThrowIfLessThan(coefficient, BigInteger.Zero);
        while (scale > 0 && coefficient != BigInteger.Zero && coefficient % 10 == BigInteger.Zero)
        {
            coefficient /= 10;
            scale--;
        }

        Coefficient = coefficient;
        Scale = coefficient == BigInteger.Zero ? 0 : scale;
    }

    /// <summary>Gets the canonical nonnegative unscaled quantity.</summary>
    /// <value>The exact coefficient with all removable base-ten trailing zeroes removed.</value>
    public BigInteger Coefficient { get; }

    /// <summary>Gets the canonical number of fractional base-ten digits.</summary>
    /// <value>Zero through 28; zero for the default exact-zero value.</value>
    public int Scale { get; }

    /// <summary>Creates an exact quantity from a nonnegative decimal value.</summary>
    /// <param name="value">The nonnegative decimal value to retain exactly.</param>
    /// <returns>The equivalent canonical quantity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static BudgetQuantity FromDecimal(decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger) (uint) bits[0] | ((BigInteger) (uint) bits[1] << 32) | ((BigInteger) (uint) bits[2] << 64);
        return new BudgetQuantity(coefficient, (bits[3] >> 16) & 0x7f);
    }

    /// <summary>Adds another exact quantity.</summary>
    /// <param name="other">The nonnegative quantity to add.</param>
    /// <returns>The canonical exact sum.</returns>
    public BudgetQuantity Add(BudgetQuantity other)
    {
        var scale = Math.Max(Scale, other.Scale);
        var left = Coefficient * BigInteger.Pow(10, scale - Scale);
        var right = other.Coefficient * BigInteger.Pow(10, scale - other.Scale);
        return new BudgetQuantity(left + right, scale);
    }

    /// <summary>Compares this exact quantity with another quantity.</summary>
    /// <param name="other">The quantity to compare.</param>
    /// <returns>A negative value, zero, or a positive value according to exact numeric ordering.</returns>
    public int CompareTo(BudgetQuantity other)
    {
        var scale = Math.Max(Scale, other.Scale);
        return (Coefficient * BigInteger.Pow(10, scale - Scale)).CompareTo(other.Coefficient * BigInteger.Pow(10, scale - other.Scale));
    }

    /// <summary>Determines whether one quantity is less than another.</summary>
    /// <param name="left">The left exact quantity.</param><param name="right">The right exact quantity.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is numerically smaller.</returns>
    public static bool operator <(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) < 0;
    /// <summary>Determines whether one quantity is less than or equal to another.</summary>
    /// <param name="left">The left exact quantity.</param><param name="right">The right exact quantity.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not numerically greater.</returns>
    public static bool operator <=(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) <= 0;
    /// <summary>Determines whether one quantity is greater than another.</summary>
    /// <param name="left">The left exact quantity.</param><param name="right">The right exact quantity.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is numerically greater.</returns>
    public static bool operator >(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) > 0;
    /// <summary>Determines whether one quantity is greater than or equal to another.</summary>
    /// <param name="left">The left exact quantity.</param><param name="right">The right exact quantity.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not numerically smaller.</returns>
    public static bool operator >=(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) >= 0;

    /// <summary>Attempts an exact conversion to decimal without rounding.</summary>
    /// <param name="value">The converted decimal value when representable; zero when conversion fails.</param>
    /// <returns><see langword="true"/> when the quantity is exactly representable as decimal.</returns>
    public bool TryGetDecimal(out decimal value)
    {
        var maximumCoefficient = (BigInteger.One << 96) - BigInteger.One;
        if (Coefficient <= maximumCoefficient)
        {
            var low = (int) (uint) (Coefficient & uint.MaxValue);
            var middle = (int) (uint) ((Coefficient >> 32) & uint.MaxValue);
            var high = (int) (uint) ((Coefficient >> 64) & uint.MaxValue);
            value = new decimal(low, middle, high, false, (byte) Scale);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Converts this quantity to decimal without rounding.</summary>
    /// <returns>The exactly representable decimal quantity.</returns>
    /// <exception cref="OverflowException">The exact quantity cannot be represented by decimal.</exception>
    public decimal ToDecimalChecked() => TryGetDecimal(out var value) ? value : throw new OverflowException("The exact budget quantity cannot be represented as decimal.");

    /// <summary>Formats the canonical exact quantity using invariant decimal notation.</summary>
    /// <returns>A culture-invariant canonical base-ten representation.</returns>
    public override string ToString()
    {
        var digits = Coefficient.ToString(CultureInfo.InvariantCulture);
        return Scale == 0 ? digits : digits.Length <= Scale
            ? "0." + new string('0', Scale - digits.Length) + digits
            : digits[..^Scale] + "." + digits[^Scale..];
    }
}
