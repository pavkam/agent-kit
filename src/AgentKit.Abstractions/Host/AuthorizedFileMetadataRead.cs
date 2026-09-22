// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes metadata observation for one resolved file target.</summary>
public sealed record AuthorizedFileMetadataRead
{
    /// <summary>Initializes authorized metadata read evidence.</summary>
    /// <param name="resolvedTarget">The resolved target to observe.</param>
    /// <param name="grant">The bounded grant for this observation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedFileMetadataRead(ResolvedFileTarget resolvedTarget, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedTarget = resolvedTarget;
        Grant = grant;
    }

    /// <summary>Gets the resolved target to observe.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the bounded observation grant.</summary>
    public SecurityGrant Grant { get; init; }
}
