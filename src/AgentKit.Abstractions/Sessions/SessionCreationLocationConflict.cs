// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a creation retry key was previously used with different original request evidence.</summary>
/// <remarks>The result never exposes the prior route. A caller must correct the retry identity or request evidence before allocating a new session identity.</remarks>
public sealed record SessionCreationLocationConflict: SessionCreationLocationResult
{
    /// <summary>Initializes a tenant-safe creation-replay conflict.</summary>
    /// <param name="safeMessage">The nonblank caller-safe conflict description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionCreationLocationConflict(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe conflict description.</summary>
    /// <value>Nonblank text that omits prior route and protected request contents.</value>
    public string SafeMessage { get; }
}
