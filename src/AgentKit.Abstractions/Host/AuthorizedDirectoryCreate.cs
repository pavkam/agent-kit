// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes creation of one directory target as a separate host effect from file writing.</summary>
public sealed record AuthorizedDirectoryCreate
{
    /// <summary>Initializes authorized directory creation evidence.</summary>
    /// <param name="resolvedTarget">The resolved directory target to create.</param>
    /// <param name="grant">The bounded grant for this creation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedDirectoryCreate(ResolvedFileTarget resolvedTarget, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedTarget = resolvedTarget;
        Grant = grant;
    }

    /// <summary>Gets the resolved directory target to create.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the bounded creation grant.</summary>
    public SecurityGrant Grant { get; init; }
}
