// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes change observation within one bounded directory root.</summary>
public sealed record AuthorizedFileWatch
{
    /// <summary>Initializes authorized watch evidence.</summary>
    /// <param name="resolvedRoot">The resolved directory root to observe.</param>
    /// <param name="grant">The bounded grant for this watch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedFileWatch(ResolvedFileTarget resolvedRoot, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedRoot = resolvedRoot;
        Grant = grant;
    }

    /// <summary>Gets the resolved directory root to observe.</summary>
    public ResolvedFileTarget ResolvedRoot { get; init; }

    /// <summary>Gets the bounded watch grant.</summary>
    public SecurityGrant Grant { get; init; }
}
