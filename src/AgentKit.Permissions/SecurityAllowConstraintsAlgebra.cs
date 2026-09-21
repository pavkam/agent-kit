// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Intersects allow and approval-conditioned policy bounds deterministically.</summary>
internal static class SecurityAllowConstraintsAlgebra
{
    /// <summary>Intersects every policy contribution and applies host ceilings last.</summary>
    /// <param name="contributions">Every non-null constraint from participating policies.</param>
    /// <param name="request">The normalized request under evaluation.</param>
    /// <param name="hostMaximumExpiry">The latest exclusive expiry the host permits.</param>
    /// <param name="hostMaximumUses">The maximum use count the host permits.</param>
    /// <param name="intersection">The merged bounds when intersection succeeds.</param>
    /// <returns><see langword="false"/> when the merged bounds are empty.</returns>
    internal static bool TryIntersect(
        IReadOnlyList<SecurityAllowConstraints> contributions,
        SecurityRequest request,
        DateTimeOffset hostMaximumExpiry,
        int hostMaximumUses,
        out SecurityAllowConstraints? intersection)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hostMaximumUses);

        ImmutableArray<ProtectedResource>? resources = null;
        SecurityEffect? effect = null;
        DateTimeOffset? notBefore = null;
        DateTimeOffset? expiresAt = null;
        int? allowedUses = null;

        foreach (var contribution in contributions)
        {
            if (!TryMergeResources(ref resources, contribution.Resources))
            {
                intersection = null;
                return false;
            }

            if (!TryMergeEffect(ref effect, contribution.Effect))
            {
                intersection = null;
                return false;
            }

            notBefore = Max(notBefore, contribution.NotBefore);
            expiresAt = Min(expiresAt, contribution.ExpiresAt);
            allowedUses = Min(allowedUses, contribution.AllowedUses);
        }

        expiresAt = Min(expiresAt, request.Deadline);
        expiresAt = Min(expiresAt, hostMaximumExpiry);
        allowedUses = Min(allowedUses, request.RequestedUses);
        allowedUses = Min(allowedUses, hostMaximumUses);

        if (notBefore is { } floor && expiresAt is { } ceiling && ceiling <= floor)
        {
            intersection = null;
            return false;
        }

        if (resources is { } narrowedResources && !request.Resources.All(narrowedResources.Contains))
        {
            intersection = null;
            return false;
        }

        if (effect is { } narrowedEffect && narrowedEffect != request.Effect)
        {
            intersection = null;
            return false;
        }

        intersection = contributions.Count == 0 && resources is null && effect is null && notBefore is null && expiresAt is null && allowedUses is null
            ? null
            : new SecurityAllowConstraints(resources, effect, notBefore, expiresAt, allowedUses);
        return true;
    }

    private static bool TryMergeResources(ref ImmutableArray<ProtectedResource>? current, ImmutableArray<ProtectedResource>? incoming)
    {
        if (incoming is null)
        {
            return true;
        }

        if (current is null)
        {
            current = incoming;
            return true;
        }

        var intersection = current.Value.Where(incoming.Value.Contains).ToImmutableArray();
        if (intersection.IsDefaultOrEmpty)
        {
            return false;
        }

        current = intersection;
        return true;
    }

    private static bool TryMergeEffect(ref SecurityEffect? current, SecurityEffect? incoming)
    {
        if (incoming is null)
        {
            return true;
        }

        if (current is null)
        {
            current = incoming;
            return true;
        }

        return current == incoming;
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right) =>
        (left, right) switch
        {
            (null, null) => null,
            (null, { } r) => r,
            ({ } l, null) => l,
            ({ } l, { } r) => l > r ? l : r,
        };

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right) =>
        (left, right) switch
        {
            (null, null) => null,
            (null, { } r) => r,
            ({ } l, null) => l,
            ({ } l, { } r) => l < r ? l : r,
        };

    private static int? Min(int? left, int? right) =>
        (left, right) switch
        {
            (null, null) => null,
            (null, { } r) => r,
            ({ } l, null) => l,
            ({ } l, { } r) => l < r ? l : r,
        };
}
