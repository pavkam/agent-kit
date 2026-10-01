// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Declares one memory event sink's identity, order, delivery strictness, lifetime, and profile filter.</summary>
public sealed record MemoryEventSinkRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="id">The sink's stable identity.</param>
    /// <param name="order">The delivery order; lower values are delivered first.</param>
    /// <param name="delivery">How strictly the sink must receive events.</param>
    /// <param name="lifetime">The service lifetime the sink instance is resolved under.</param>
    /// <param name="profiles">The profiles the sink observes, or default for every profile.</param>
    /// <exception cref="ArgumentException">The identity is blank, or a declared profile is blank or repeated.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> or <paramref name="lifetime"/> is undefined.</exception>
    public MemoryEventSinkRegistration(
        ComponentId id,
        int order,
        MemoryEventDelivery delivery,
        ServiceLifetime lifetime,
        ImmutableArray<MemoryProfileKey> profiles = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        var declared = profiles.IsDefault ? [] : profiles;
        var seen = new HashSet<MemoryProfileKey>();
        foreach (var profile in declared)
        {
            if (string.IsNullOrWhiteSpace(profile.Value) || !seen.Add(profile))
            {
                throw new ArgumentException("Declared memory profiles must be initialized and unique.", nameof(profiles));
            }
        }

        Id = id;
        Order = order;
        Delivery = delivery;
        Lifetime = lifetime;
        Profiles = declared;
    }

    /// <summary>Gets the sink's stable identity.</summary>
    public ComponentId Id { get; }

    /// <summary>Gets the delivery order.</summary>
    public int Order { get; }

    /// <summary>Gets how strictly the sink must receive events.</summary>
    public MemoryEventDelivery Delivery { get; }

    /// <summary>Gets the service lifetime the sink instance is resolved under.</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>Gets the profiles the sink observes; empty for every profile.</summary>
    public ImmutableArray<MemoryProfileKey> Profiles { get; }

    /// <summary>Determines whether the sink observes a profile.</summary>
    /// <param name="profile">The profile an event concerns.</param>
    /// <returns><see langword="true"/> when the sink observes every profile or names this one.</returns>
    public bool Observes(MemoryProfileKey profile) => Profiles.IsEmpty || Profiles.Contains(profile);

    /// <summary>Determines whether another registration is identical, comparing profiles by content.</summary>
    /// <param name="other">The registration to compare.</param>
    /// <returns><see langword="true"/> when every value matches.</returns>
    public bool Equals(MemoryEventSinkRegistration? other) =>
        other is not null && Id == other.Id && Order == other.Order && Delivery == other.Delivery
        && Lifetime == other.Lifetime && Profiles.SequenceEqual(other.Profiles);

    /// <summary>Returns a hash code consistent with <see cref="Equals(MemoryEventSinkRegistration?)"/>.</summary>
    /// <returns>A hash over every value.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Order);
        hash.Add(Delivery);
        hash.Add(Lifetime);
        foreach (var profile in Profiles)
        {
            hash.Add(profile);
        }

        return hash.ToHashCode();
    }
}
