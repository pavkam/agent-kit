// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes the synchronously materialized run-profile bindings available to composition validation.</summary>
public sealed record AgentRunProfilePublicationSnapshot
{
    /// <summary>Initializes a readiness snapshot over immutable exact bindings.</summary>
    /// <param name="publications">The initialized collection of non-null publications.</param>
    /// <exception cref="ArgumentException"><paramref name="publications"/> is default or contains null.</exception>
    public AgentRunProfilePublicationSnapshot(ImmutableArray<AgentRunProfilePublication> publications)
    {
        ArgumentException.ThrowIfContainsNull(publications);
        Publications = publications;
    }

    /// <summary>Gets the immutable exact run-profile publications.</summary>
    public ImmutableArray<AgentRunProfilePublication> Publications { get; }
}
