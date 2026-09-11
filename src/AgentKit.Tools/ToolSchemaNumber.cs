// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Globalization;
using System.Numerics;
using System.Text.Json;

/// <summary>Represents an exact finite JSON decimal without expanding its exponent into a large coefficient.</summary>
/// <remarks>Numbers are normalized only for comparison; caller JSON remains unchanged. Parsing cost is charged by the calling bounded processor.</remarks>
internal readonly record struct ToolSchemaNumber
{
    private readonly int _sign;
    private readonly string _digits;
    private readonly BigInteger _exponent;

    private ToolSchemaNumber(int sign, string digits, BigInteger exponent)
    {
        Debug.Assert(sign is -1 or 0 or 1 && digits.Length > 0, "Normalized decimal parts are established by Parse.");
        _sign = sign; _digits = digits; _exponent = exponent;
    }

    /// <summary>Parses an initialized JSON number into exact sign, significant digits, and decimal exponent.</summary>
    /// <param name="number">An initialized JSON number whose raw length has already been bounded and charged.</param>
    /// <returns>An exact decimal representation with trailing coefficient zeroes removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="number"/> is not a number.</exception>
    internal static ToolSchemaNumber Parse(JsonElement number)
    {
        ArgumentException.ThrowIfNotEqual(number.ValueKind, JsonValueKind.Number, nameof(number));
        var raw = number.GetRawText();
        var index = raw.IndexOfAny('e', 'E');
        var significand = index < 0 ? raw : raw[..index];
        var exponent = index < 0 ? BigInteger.Zero : BigInteger.Parse(raw.AsSpan(index + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var point = significand.IndexOf('.');
        if (point >= 0) { exponent -= significand.Length - point - 1; }
        var digits = significand.Replace("-", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) { return new(0, "0", BigInteger.Zero); }
        var trimmed = digits.TrimEnd('0');
        exponent += digits.Length - trimmed.Length;
        return new(raw[0] == '-' ? -1 : 1, trimmed, exponent);
    }

    /// <summary>Gets whether the exact value has no fractional part.</summary>
    /// <value>True for zero or a normalized nonnegative decimal exponent, independent of lexical spelling.</value>
    internal bool IsInteger => _sign == 0 || _exponent.Sign >= 0;

    /// <summary>Gets whether the exact value is nonnegative.</summary>
    /// <value>True for positive values and every spelling of zero.</value>
    internal bool IsNonnegative => _sign >= 0;

    /// <summary>Compares exact mathematical decimal values without binary floating-point rounding or exponent expansion.</summary>
    /// <param name="other">An initialized normalized decimal created by Parse.</param>
    /// <returns>A negative, zero, or positive comparison result.</returns>
    /// <remarks>Callers charge the bounded digit/exponent lengths before comparison; default values are outside the internal contract.</remarks>
    internal int CompareTo(ToolSchemaNumber other)
    {
        Debug.Assert(_digits is not null && other._digits is not null, "Only normalized parsed numbers are compared.");
        if (_sign != other._sign) { return _sign.CompareTo(other._sign); }
        if (_sign == 0) { return 0; }
        var magnitude = (_exponent + _digits.Length).CompareTo(other._exponent + other._digits.Length);
        if (magnitude != 0) { return _sign * magnitude; }
        for (var i = 0; i < Math.Max(_digits.Length, other._digits.Length); i++)
        {
            var left = i < _digits.Length ? _digits[i] : '0';
            var right = i < other._digits.Length ? other._digits[i] : '0';
            if (left != right) { return _sign * left.CompareTo(right); }
        }
        return 0;
    }
}
