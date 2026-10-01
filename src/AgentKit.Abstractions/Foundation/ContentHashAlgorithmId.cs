// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The stable identity of one content-hash algorithm, such as <c>sha256</c>.</summary>
/// <remarks>
/// This type is an immutable value object with ordinal textual equality. A default instance can exist in memory but
/// is never a valid algorithm identity; every public boundary rejects it.
/// </remarks>
public readonly record struct ContentHashAlgorithmId
{
    /// <summary>Initializes a validated <see cref="ContentHashAlgorithmId"/> value.</summary>
    /// <param name="value">The nonblank identity text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace-only.</exception>
    public ContentHashAlgorithmId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the nonblank identity text.</summary>
    public string Value { get; }

    /// <summary>Returns the identity text.</summary>
    /// <returns>The identity text, or an empty string for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
