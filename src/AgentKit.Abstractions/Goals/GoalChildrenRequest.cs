// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a store or coordinator for one page of a goal's children in recorded child-ordinal order.</summary>
public sealed record GoalChildrenRequest
{
    /// <summary>Initializes a validated children request.</summary>
    /// <param name="profile">The captured profile the parent runs under.</param>
    /// <param name="parentId">The parent goal.</param>
    /// <param name="afterOrdinal">The exclusive child-ordinal cursor; zero starts at the first child.</param>
    /// <param name="limit">The positive page size.</param>
    /// <param name="grant">The single-use read grant.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentId"/> is default, <paramref name="afterOrdinal"/> is negative, or <paramref name="limit"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The grant lacks captured authorization.</exception>
    public GoalChildrenRequest(GoalProfileReference profile, GoalId parentId, long afterOrdinal, int limit, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(parentId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(afterOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Profile = profile;
        ParentId = parentId;
        AfterOrdinal = afterOrdinal;
        Limit = limit;
        Grant = grant;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the parent goal.</summary>
    public GoalId ParentId { get; }

    /// <summary>Gets the exclusive child-ordinal cursor.</summary>
    public long AfterOrdinal { get; }

    /// <summary>Gets the page size.</summary>
    public int Limit { get; }

    /// <summary>Gets the single-use read grant.</summary>
    public SecurityGrant Grant { get; }
}
