// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one keyed query-rewriter registration.</summary>
/// <remarks>Values are preserved exactly without trimming and compare with ordinal, case-sensitive semantics.</remarks>
public readonly record struct QueryRewriterKey
{
    /// <summary>Initializes a validated query-rewriter selection key.</summary>
    /// <param name="value">The non-blank canonical rewriter key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public QueryRewriterKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical rewriter key text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
