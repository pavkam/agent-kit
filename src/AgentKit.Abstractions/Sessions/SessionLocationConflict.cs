// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an existing location pins a session to a different store.</summary>
/// <remarks>Callers must continue using the existing route after separately authorizing its store access; they must not overwrite or probe a new default store.</remarks>
public sealed record SessionLocationConflict: SessionDirectoryWriteResult
{
    /// <summary>Initializes a pinned-route conflict outcome.</summary>
    /// <param name="existing">The non-null existing authoritative location.</param>
    /// <param name="requestedStoreKey">The nonblank conflicting requested store key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="requestedStoreKey"/> is blank.</exception>
    public SessionLocationConflict(SessionLocation existing, SessionStoreKey requestedStoreKey)
    {
        ArgumentNullException.ThrowIfNull(existing); ArgumentException.ThrowIfNullOrWhiteSpace(requestedStoreKey.Value, nameof(requestedStoreKey));
        Existing = existing; RequestedStoreKey = requestedStoreKey;
    }

    /// <summary>Gets the existing authoritative route.</summary><value>The non-null pinned location.</value>
    public SessionLocation Existing { get; }
    /// <summary>Gets the requested conflicting store key.</summary><value>The nonblank store key that was not recorded.</value>
    public SessionStoreKey RequestedStoreKey { get; }
}
