// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable published revision of a query rewriter.</summary>
public readonly record struct QueryRewriterVersion
{
    /// <summary>Initializes a non-blank rewriter version.</summary>
    /// <param name="value">The stable version value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public QueryRewriterVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable version value.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
