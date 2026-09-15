// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Persists the principal owning one session address.</summary>
internal sealed record SqliteSessionDirectoryOwner
{
    /// <summary>Creates one owner row.</summary>
    /// <param name="address">The complete session address.</param>
    /// <param name="principalId">The authenticated owner.</param>
    public SqliteSessionDirectoryOwner(SessionAddress address, PrincipalId principalId)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId.Value, nameof(principalId));
        Address = address; PrincipalId = principalId;
    }
    /// <summary>Gets the owned address.</summary>
    public SessionAddress Address { get; }
    /// <summary>Gets the owning principal.</summary>
    public PrincipalId PrincipalId { get; }
}
