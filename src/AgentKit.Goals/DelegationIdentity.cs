// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using System.Security.Cryptography;
using System.Text;

/// <summary>Derives the identities a delegation must reproduce exactly on every retry.</summary>
/// <remarks>
/// A retried delegation carries the same idempotency key but arrives later, possibly from a recovered run, and the caller
/// allocates a fresh delegation identity each time. The child goal, the delegation record, the attempt, and the store
/// creation key are therefore derived from the parent goal and that key alone, which makes the retry resolve to the one
/// existing child instead of creating a duplicate.
/// </remarks>
internal static class DelegationIdentity
{
    /// <summary>Derives the child goal identity.</summary>
    /// <param name="parent">The parent goal.</param>
    /// <param name="key">The delegation idempotency key.</param>
    /// <returns>A stable non-default goal identity.</returns>
    internal static GoalId ChildGoalId(GoalId parent, IdempotencyKey key) => new(Derive("child-goal", parent.ToString(), key.Value));

    /// <summary>Derives the persisted delegation identity.</summary>
    /// <param name="parent">The parent goal.</param>
    /// <param name="key">The delegation idempotency key.</param>
    /// <returns>A stable non-default delegation identity.</returns>
    internal static DelegationId DelegationId(GoalId parent, IdempotencyKey key) => new(Derive("delegation", parent.ToString(), key.Value));

    /// <summary>Derives the first attempt identity for a goal and request key.</summary>
    /// <param name="goal">The goal receiving the attempt.</param>
    /// <param name="key">The attempt idempotency key.</param>
    /// <returns>A stable non-default attempt identity.</returns>
    internal static GoalAttemptId AttemptId(GoalId goal, IdempotencyKey key) => new(Derive("attempt", goal.ToString(), key.Value));

    /// <summary>Builds the store creation key for a delegated child.</summary>
    /// <param name="parent">The parent goal.</param>
    /// <param name="key">The delegation idempotency key.</param>
    /// <returns>The key a store indexes the child creation under.</returns>
    internal static IdempotencyKey CreationKey(GoalId parent, IdempotencyKey key) => new($"agentkit.goals.delegation:{parent}:{key.Value}");

    private static Guid Derive(string purpose, string first, string second)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}\n{first}\n{second}"));
        var value = new Guid(hash.AsSpan(0, 16));
        return value == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : value;
    }
}
