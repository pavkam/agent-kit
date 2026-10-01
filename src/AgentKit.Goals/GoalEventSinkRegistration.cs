// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Declares the stable identity, order, delivery guarantee, lifetime, and profile scope of one goal-event sink.</summary>
public sealed record GoalEventSinkRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="id">The non-blank sink identity.</param>
    /// <param name="order">The ascending delivery order.</param>
    /// <param name="delivery">The defined delivery guarantee.</param>
    /// <param name="lifetime">The defined container lifetime of the sink.</param>
    /// <param name="profiles">The profiles the sink observes; default or empty observes every profile.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is blank or a profile is blank or repeated.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> or <paramref name="lifetime"/> is undefined.</exception>
    public GoalEventSinkRegistration(
        ComponentId id,
        int order,
        GoalEventDelivery delivery,
        ServiceLifetime lifetime,
        ImmutableArray<GoalProfileKey> profiles = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        var declared = profiles.IsDefault ? [] : profiles;
        var seen = new HashSet<GoalProfileKey>();
        foreach (var profile in declared)
        {
            if (string.IsNullOrWhiteSpace(profile.Value) || !seen.Add(profile))
            {
                throw new ArgumentException("Declared goal profiles must be initialized and unique.", nameof(profiles));
            }
        }

        Id = id;
        Order = order;
        Delivery = delivery;
        Lifetime = lifetime;
        Profiles = declared;
    }

    /// <summary>Gets the sink identity.</summary>
    public ComponentId Id { get; }

    /// <summary>Gets the ascending delivery order.</summary>
    public int Order { get; }

    /// <summary>Gets the delivery guarantee.</summary>
    public GoalEventDelivery Delivery { get; }

    /// <summary>Gets the container lifetime of the sink.</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>Gets the profiles the sink observes; empty observes every profile.</summary>
    public ImmutableArray<GoalProfileKey> Profiles { get; }

    /// <summary>Determines whether the sink observes events of one profile.</summary>
    /// <param name="profile">The event's captured profile.</param>
    /// <returns><see langword="true"/> when the sink observes every profile or names this one.</returns>
    public bool Observes(GoalProfileKey profile) => Profiles.IsEmpty || Profiles.Contains(profile);

    /// <inheritdoc/>
    public bool Equals(GoalEventSinkRegistration? other) =>
        other is not null && Id == other.Id && Order == other.Order && Delivery == other.Delivery
        && Lifetime == other.Lifetime && Profiles.SequenceEqual(other.Profiles);

    /// <inheritdoc/>
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
