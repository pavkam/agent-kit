// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal coordinator to authorize and read one page of a goal's children in recorded ordinal order.</summary>
public sealed record GoalChildrenCommand
{
    /// <summary>Initializes a validated children command.</summary>
    /// <param name="profile">The captured profile the parent runs under.</param>
    /// <param name="parentId">The parent goal.</param>
    /// <param name="afterOrdinal">The exclusive child-ordinal cursor; zero starts at the first child.</param>
    /// <param name="limit">The positive page size.</param>
    /// <param name="authorization">The captured authorization whose scope owns the parent.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentId"/> is default, <paramref name="afterOrdinal"/> is negative, or <paramref name="limit"/> is not positive.</exception>
    public GoalChildrenCommand(GoalProfileReference profile, GoalId parentId, long afterOrdinal, int limit, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(parentId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(afterOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentNullException.ThrowIfNull(authorization);
        Profile = profile;
        ParentId = parentId;
        AfterOrdinal = afterOrdinal;
        Limit = limit;
        Authorization = authorization;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the parent goal.</summary>
    public GoalId ParentId { get; }

    /// <summary>Gets the exclusive child-ordinal cursor.</summary>
    public long AfterOrdinal { get; }

    /// <summary>Gets the page size.</summary>
    public int Limit { get; }

    /// <summary>Gets the captured authorization whose scope owns the parent.</summary>
    public SecurityAuthorizationContext Authorization { get; }
}
