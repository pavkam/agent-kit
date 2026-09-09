// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the mapper or effect boundary that produced a portable error using exact ordinal text.</summary>
/// <remarks>
/// An origin is extensible provenance. It is not an error category, authority, resolver key, activation key, or service
/// identity, and resolving an origin never executes code.
/// </remarks>
public readonly record struct ErrorOrigin
{
    /// <summary>Creates mapper provenance without case folding or whitespace normalization.</summary>
    /// <param name="value">The nonblank ordinal origin identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ErrorOrigin(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact mapper or boundary provenance identity.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact origin without culture-sensitive conversion.</summary>
    /// <returns>The origin text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
