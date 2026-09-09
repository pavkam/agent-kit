// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one context source within its namespace using exact ordinal text.</summary>
/// <remarks>The CLR default value is an invalid source-identity sentinel.</remarks>
public readonly record struct ContextSourceKey
{
    /// <summary>Creates a source key without case folding or whitespace normalization.</summary>
    /// <param name="value">The nonblank ordinal key text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ContextSourceKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact source key supplied by the publisher.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact key without culture-sensitive conversion.</summary>
    /// <returns>The key text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
