// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a non-empty content-search pattern and its pinned interpretation.</summary>
public readonly record struct FileSearchPattern
{
    /// <summary>The maximum accepted UTF-16 pattern length.</summary>
    public const int MaximumLength = 4_096;

    /// <summary>Initializes a validated search pattern.</summary>
    /// <param name="value">The non-empty literal or regular-expression text.</param>
    /// <param name="kind">The pinned pattern interpretation.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank or is not supported by the non-backtracking engine.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> exceeds <see cref="MaximumLength"/> or <paramref name="kind"/> is undefined.</exception>
    public FileSearchPattern(string value, FileSearchPatternKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaximumLength, nameof(value));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        if (kind == FileSearchPatternKind.RegularExpression)
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(
                    value,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant
                        | System.Text.RegularExpressions.RegexOptions.NonBacktracking);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                throw new ArgumentException(
                    "Pattern is invalid or unsupported by the pinned non-backtracking regular-expression engine.",
                    nameof(value),
                    exception);
            }
        }

        Value = value;
        Kind = kind;
    }

    /// <summary>Gets the exact pattern text.</summary>
    public string Value { get; }

    /// <summary>Gets the pinned pattern interpretation.</summary>
    public FileSearchPatternKind Kind { get; }

    /// <summary>Returns the exact pattern text.</summary>
    public override string ToString() => Value;
}
