// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Decides whether two registrations describe the same strategy placement.</summary>
internal static class CompactionRegistrationEquality
{
    /// <summary>Compares descriptor, order, before/after sets, and lifetime by value.</summary>
    /// <param name="left">The first registration.</param>
    /// <param name="right">The second registration.</param>
    /// <returns><see langword="true"/> when both registrations are interchangeable.</returns>
    internal static bool Equivalent(CompactionStrategyRegistration left, CompactionStrategyRegistration right)
    {
        Debug.Assert(left is not null && right is not null, "Both registrations are validated non-null by the entry points.");
        return left.Descriptor == right.Descriptor
            && left.Order == right.Order
            && left.Lifetime == right.Lifetime
            && left.Before.ToHashSet().SetEquals(right.Before)
            && left.After.ToHashSet().SetEquals(right.After);
    }
}
