// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Registration metadata for one compaction event sink.</summary>
public sealed record CompactionEventSinkRegistration
{
    /// <summary>Initializes a new instance of the <see cref="CompactionEventSinkRegistration"/> record.</summary>
    public CompactionEventSinkRegistration(
        CompactionEventSinkId id,
        int order,
        CompactionEventDelivery delivery,
        ServiceLifetime lifetime)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        Id = id;
        Order = order;
        Delivery = delivery;
        Lifetime = lifetime;
    }

    /// <summary>Gets the sink identity.</summary>
    public CompactionEventSinkId Id { get; }

    /// <summary>Gets the deterministic dispatch order.</summary>
    public int Order { get; }

    /// <summary>Gets the delivery semantics.</summary>
    public CompactionEventDelivery Delivery { get; }

    /// <summary>Gets the DI lifetime.</summary>
    public ServiceLifetime Lifetime { get; }
}
