// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the exact query rewriter key and version a profile captured or a rewritten query was produced by.</summary>
public sealed record QueryRewriterReference
{
    /// <summary>Initializes a validated reference.</summary>
    /// <param name="key">The rewriter's registration key.</param>
    /// <param name="version">The rewriter's version.</param>
    /// <exception cref="ArgumentException">The key or version is default or blank.</exception>
    public QueryRewriterReference(QueryRewriterKey key, QueryRewriterVersion version)
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

    /// <summary>Determines whether a descriptor identifies this exact key and version.</summary>
    /// <param name="descriptor">The descriptor to compare.</param>
    /// <returns><see langword="true"/> when the key and version both match.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public bool Matches(QueryRewriterDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Key == descriptor.Key && Version == descriptor.Version;
    }
}
