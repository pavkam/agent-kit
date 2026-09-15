// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Indexes one loaded <see cref="SqliteSessionDirectoryState"/> for a single directory operation.</summary>
/// <remarks>
/// Each directory read or mutation builds its own projection from the state it loaded, mutates only that projection,
/// and captures the result back into the same transaction snapshot. Projections are never shared between operations,
/// so a concurrent read can never replace a writer's in-flight changes; SQLite immediate transactions arbitrate
/// concurrent writers. Instances are not thread-safe and are confined to the operation that created them.
/// </remarks>
internal sealed class SqliteSessionDirectoryProjection
{
    /// <summary>Initializes an indexed projection from one complete committed state.</summary>
    /// <param name="state">The non-null state loaded from the directory table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    internal SqliteSessionDirectoryProjection(SqliteSessionDirectoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var location in state.Locations)
        {
            Locations.Add(location.Address, location);
        }

        foreach (var owner in state.Owners)
        {
            Owners.Add(owner.Address, owner.PrincipalId);
        }

        foreach (var route in state.CreationRoutes)
        {
            CreationRoutes.Add(new(route.Location.TenantId, route.Request.AgentId, route.Request.IdempotencyKey), route);
        }

        foreach (var route in state.WriteRoutes)
        {
            WriteRoutes.Add(new(route.Request.Context.Identity.TenantId, route.Location.Address, route.Request.IdempotencyKey), route);
        }
    }

    /// <summary>Gets authoritative locations keyed by complete session address.</summary>
    internal Dictionary<SessionAddress, SessionLocation> Locations { get; } = [];

    /// <summary>Gets the recording principal for every address in <see cref="Locations"/>.</summary>
    internal Dictionary<SessionAddress, PrincipalId> Owners { get; } = [];

    /// <summary>Gets exact creation retry routes keyed by tenant, agent, and creation idempotency key.</summary>
    internal Dictionary<CreationRouteKey, CreationRoute> CreationRoutes { get; } = [];

    /// <summary>Gets exact directory-write retry routes keyed by tenant, address, and write idempotency key.</summary>
    internal Dictionary<DirectoryWriteKey, DirectoryWriteRoute> WriteRoutes { get; } = [];

    /// <summary>Replaces the collections of <paramref name="state"/> with this projection's current contents.</summary>
    /// <param name="state">The non-null transaction snapshot that will be serialized and committed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    internal void CaptureInto(SqliteSessionDirectoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Locations = [.. Locations.Values];
        state.Owners = [.. Owners.Select(static pair => new SqliteSessionDirectoryOwner(pair.Key, pair.Value))];
        state.CreationRoutes = [.. CreationRoutes.Values];
        state.WriteRoutes = [.. WriteRoutes.Values];
    }
}
