// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes streaming enumeration of one directory target.</summary>
public sealed record AuthorizedDirectoryEnumeration
{
    /// <summary>Initializes authorized directory enumeration evidence.</summary>
    /// <param name="resolvedTarget">The resolved directory target.</param>
    /// <param name="grant">The bounded grant for this enumeration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedDirectoryEnumeration(ResolvedFileTarget resolvedTarget, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedTarget = resolvedTarget;
        Grant = grant;
    }

    /// <summary>Gets the resolved directory target.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the bounded enumeration grant.</summary>
    public SecurityGrant Grant { get; init; }
}
