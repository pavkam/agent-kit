// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the immutable run profile found for exact coordinates.</summary>
public sealed record AgentRunProfilePublicationFound: AgentRunProfilePublicationResult
{
    /// <summary>Initializes a successful exact read.</summary>
    /// <param name="publication">The non-null immutable publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publication"/> is null.</exception>
    public AgentRunProfilePublicationFound(AgentRunProfilePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        Publication = publication;
    }

    /// <summary>Gets the exact immutable publication.</summary>
    public AgentRunProfilePublication Publication { get; }
}
