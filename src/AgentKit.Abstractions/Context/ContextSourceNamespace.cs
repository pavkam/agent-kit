// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the namespace that owns a context source key using exact ordinal text.</summary>
/// <remarks>The CLR default value is an invalid source-identity sentinel.</remarks>
public readonly record struct ContextSourceNamespace
{
    /// <summary>Creates a source namespace without case folding or whitespace normalization.</summary>
    /// <param name="value">The nonblank ordinal namespace text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public ContextSourceNamespace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact namespace text supplied by the publisher.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact namespace without culture-sensitive conversion.</summary>
    /// <returns>The namespace text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
