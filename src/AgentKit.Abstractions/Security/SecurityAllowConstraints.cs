// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one policy's optional intersecting bounds for an allow or approval-conditioned proposal.</summary>
/// <remarks>Every member is optional: an absent member imposes no additional bound from this policy. The authority intersects every contributing policy's constraints; an empty resulting intersection denies the request.</remarks>
public sealed record SecurityAllowConstraints
{
    /// <summary>Initializes an allow-constraint contribution.</summary>
    /// <param name="resources">The narrowed resource set this policy permits, or null to impose no narrowing.</param>
    /// <param name="effect">The narrowed effect this policy permits, or null to impose no narrowing.</param>
    /// <param name="notBefore">The earliest instant this policy permits, or null to impose no floor.</param>
    /// <param name="expiresAt">The latest exclusive expiry this policy permits, or null to impose no ceiling.</param>
    /// <param name="allowedUses">The maximum use count this policy permits, or null to impose no ceiling.</param>
    /// <exception cref="ArgumentException"><paramref name="resources"/> has a value that is default or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="allowedUses"/> has a value that is not positive, or <paramref name="expiresAt"/> and <paramref name="notBefore"/> both have values where the expiry is not later than the floor.</exception>
    public SecurityAllowConstraints(
        ImmutableArray<ProtectedResource>? resources,
        SecurityEffect? effect,
        DateTimeOffset? notBefore,
        DateTimeOffset? expiresAt,
        int? allowedUses)
    {
        if (resources is { } resourceValues)
        {
            ArgumentException.ThrowIfDefaultOrEmpty(resourceValues, nameof(resources));
        }

        if (effect is { } effectValue)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(effectValue, nameof(effect));
        }

        if (allowedUses is { } usesValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usesValue, nameof(allowedUses));
        }

        if (notBefore is { } notBeforeValue && expiresAt is { } expiresAtValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtValue, notBeforeValue, nameof(expiresAt));
        }

        Resources = resources;
        Effect = effect;
        NotBefore = notBefore;
        ExpiresAt = expiresAt;
        AllowedUses = allowedUses;
    }

    /// <summary>Gets the narrowed resource set, or null when this policy imposes no narrowing.</summary>
    public ImmutableArray<ProtectedResource>? Resources { get; }
    /// <summary>Gets the narrowed effect, or null when this policy imposes no narrowing.</summary>
    public SecurityEffect? Effect { get; }
    /// <summary>Gets the earliest permitted instant, or null when this policy imposes no floor.</summary>
    public DateTimeOffset? NotBefore { get; }
    /// <summary>Gets the latest permitted exclusive expiry, or null when this policy imposes no ceiling.</summary>
    public DateTimeOffset? ExpiresAt { get; }
    /// <summary>Gets the maximum permitted use count, or null when this policy imposes no ceiling.</summary>
    public int? AllowedUses { get; }

    /// <summary>Compares every optional bound, including ordered resources when present.</summary>
    /// <param name="other">The constraints to compare with.</param>
    /// <returns><see langword="true"/> when every bound matches exactly.</returns>
    public bool Equals(SecurityAllowConstraints? other) =>
        other is not null
        && NullableResourcesEqual(Resources, other.Resources)
        && Effect == other.Effect
        && NotBefore == other.NotBefore
        && ExpiresAt == other.ExpiresAt
        && AllowedUses == other.AllowedUses;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        if (Resources is { } resources)
        {
            foreach (var resource in resources)
            {
                hash.Add(resource);
            }
        }

        hash.Add(Effect);
        hash.Add(NotBefore);
        hash.Add(ExpiresAt);
        hash.Add(AllowedUses);
        return hash.ToHashCode();
    }

    private static bool NullableResourcesEqual(ImmutableArray<ProtectedResource>? left, ImmutableArray<ProtectedResource>? right) =>
        (left, right) switch
        {
            (null, null) => true,
            ({ } leftValues, { } rightValues) => leftValues.SequenceEqual(rightValues),
            _ => false,
        };
}
