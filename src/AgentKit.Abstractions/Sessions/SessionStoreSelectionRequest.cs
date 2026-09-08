// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies the context, profile, and authoritative location used to resolve an existing session's store.</summary>
/// <remarks>The selector maps only <see cref="Location"/>'s pinned key against its explicitly injected store set. It neither re-queries the directory nor receives its grant.</remarks>
public sealed record SessionStoreSelectionRequest
{
    /// <summary>Initializes an existing-store selection request.</summary>
    /// <param name="context">The non-null operation context requesting access.</param>
    /// <param name="profile">The non-null compiled session profile.</param>
    /// <param name="location">The non-null directory location.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public SessionStoreSelectionRequest(SessionOperationContext context, SessionProfileSnapshot profile, SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(location);
        Context = context;
        Profile = profile;
        Location = location;
    }

    /// <summary>Gets the requesting operation context.</summary><value>The non-null context evaluated against the location.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the compiled session profile.</summary><value>The non-null immutable profile retained for the operation.</value>
    public SessionProfileSnapshot Profile { get; }
    /// <summary>Gets the authoritative directory location.</summary><value>The non-null pinned routing location.</value>
    public SessionLocation Location { get; }
}
