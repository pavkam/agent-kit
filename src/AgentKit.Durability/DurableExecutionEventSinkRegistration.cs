// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registration metadata for one durable execution event sink.</summary>
/// <remarks>
/// Sinks are additive and ordered. The dispatcher invokes them in ascending <see cref="Order"/>, restricted to the
/// declared <see cref="Profiles"/> filter, and resolves each one through its declared <see cref="Lifetime"/> for every
/// publication. Registration observes durable execution; it never authorizes it or alters a durable record.
/// </remarks>
public sealed record DurableExecutionEventSinkRegistration
{
    /// <summary>Initializes immutable sink registration metadata.</summary>
    /// <param name="id">The nondefault sink registration identity.</param>
    /// <param name="order">The deterministic dispatch order, where lower values are invoked first.</param>
    /// <param name="delivery">Whether delivery is observational or required.</param>
    /// <param name="lifetime">The DI lifetime under which the sink instance is resolved.</param>
    /// <param name="profiles">
    /// The durability profiles this sink observes. An empty array observes every profile; a non-empty array restricts
    /// dispatch to contexts whose captured profile key appears in it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default, or <paramref name="delivery"/> or <paramref name="lifetime"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="profiles"/> is a default array or contains a default or duplicate key.</exception>
    public DurableExecutionEventSinkRegistration(
        DurableExecutionEventSinkId id,
        int order,
        DurableExecutionEventDelivery delivery,
        ServiceLifetime lifetime,
        ImmutableArray<DurabilityProfileKey> profiles = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        var declared = profiles.IsDefault ? [] : profiles;
        var seen = new HashSet<DurabilityProfileKey>();
        foreach (var profile in declared)
        {
            if (profile == default || !seen.Add(profile))
            {
                throw new ArgumentException("Declared durability profiles must be initialized and unique.", nameof(profiles));
            }
        }

        Id = id;
        Order = order;
        Delivery = delivery;
        Lifetime = lifetime;
        Profiles = declared;
    }

    /// <summary>Gets the sink registration identity.</summary>
    public DurableExecutionEventSinkId Id { get; }

    /// <summary>Gets the deterministic dispatch order.</summary>
    /// <value>Lower values are invoked first; equal values retain registration order.</value>
    public int Order { get; }

    /// <summary>Gets whether delivery is observational or required.</summary>
    public DurableExecutionEventDelivery Delivery { get; }

    /// <summary>Gets the DI lifetime under which the sink is resolved for each publication.</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>Gets the durability profiles this sink observes.</summary>
    /// <value>An empty array observes every profile.</value>
    public ImmutableArray<DurabilityProfileKey> Profiles { get; }

    /// <summary>Determines whether this registration observes one captured durability profile.</summary>
    /// <param name="profile">The captured profile key from a durable execution context.</param>
    /// <returns><see langword="true"/> when the filter is empty or contains <paramref name="profile"/>.</returns>
    public bool Observes(DurabilityProfileKey profile) => Profiles.IsEmpty || Profiles.Contains(profile);
}
