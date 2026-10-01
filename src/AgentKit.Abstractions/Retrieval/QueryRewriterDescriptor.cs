// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one query rewriter and the exact version of its behavior.</summary>
public sealed record QueryRewriterDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="key">The rewriter's registration key.</param>
    /// <param name="version">The rewriter's version.</param>
    /// <exception cref="ArgumentException">The key or version is default or blank.</exception>
    public QueryRewriterDescriptor(QueryRewriterKey key, QueryRewriterVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        Key = key;
        Version = version;
    }

    /// <summary>Gets the rewriter's registration key.</summary>
    public QueryRewriterKey Key { get; }

    /// <summary>Gets the rewriter's version.</summary>
    public QueryRewriterVersion Version { get; }
}
