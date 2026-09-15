// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the optional expiry boundary of already-published context evidence.</summary>
/// <remarks>A null expiry pins the value only to its captured source publication or lifecycle; it never makes content process-global forever.</remarks>
public sealed record ContextFreshness
{
    /// <summary>Creates freshness evidence with an optional wall-clock expiry.</summary>
    /// <param name="expiresAt">The expiry instant, or null to pin freshness to the captured publication or lifecycle.</param>
    public ContextFreshness(DateTimeOffset? expiresAt) => ExpiresAt = expiresAt;

    /// <summary>Gets the instant after which the captured content is stale.</summary>
    /// <value>An expiry instant, or null when freshness is pinned to captured publication evidence.</value>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Gets freshness pinned to the already captured source publication or lifecycle.</summary>
    /// <value>An immutable freshness value with no wall-clock expiry.</value>
    public static ContextFreshness Pinned { get; } = new(expiresAt: null);
}
