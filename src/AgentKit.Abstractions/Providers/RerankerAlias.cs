// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An application-facing selection key for one configured reranker.</summary>
public readonly record struct RerankerAlias
{
    /// <summary>Initializes a reranker alias.</summary>
    /// <param name="value">The non-empty alias text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null or whitespace.</exception>
    public RerankerAlias(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the alias text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
