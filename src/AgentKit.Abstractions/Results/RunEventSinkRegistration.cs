// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares one <see cref="IRunEventSink"/>'s stable identity, delivery requirement, and fan-out position.</summary>
/// <remarks>
/// Registrations are additive and keyed by <see cref="SinkName"/>: an <c>AddRunEventSink</c> registration whose
/// name is already registered with a different <see cref="Delivery"/> or a different sink implementation type
/// fails composition rather than silently replacing the earlier registration. Repeating an identical
/// registration is idempotent. <see cref="Order"/> is the deterministic fan-out order among every registered
/// sink for one publisher composition; ties are broken by registration order.
/// </remarks>
public sealed record RunEventSinkRegistration
{
    /// <summary>Initializes one sink registration.</summary>
    /// <param name="sinkName">The non-blank name unique among every sink registered for the same publisher composition.</param>
    /// <param name="delivery">The defined delivery requirement this sink is held to.</param>
    /// <param name="order">The sink's position in the deterministic fan-out order; lower values run first.</param>
    /// <exception cref="ArgumentException"><paramref name="sinkName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delivery"/> is undefined.</exception>
    public RunEventSinkRegistration(string sinkName, RunEventDelivery delivery, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sinkName);
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        SinkName = sinkName;
        Delivery = delivery;
        Order = order;
    }

    /// <summary>Gets the name unique among every sink registered for the same publisher composition.</summary>
    /// <value>A non-blank stable identity used for duplicate-conflict validation and diagnostics.</value>
    public string SinkName { get; }

    /// <summary>Gets the delivery requirement this sink is held to.</summary>
    /// <value>A defined <see cref="RunEventDelivery"/> value.</value>
    public RunEventDelivery Delivery { get; }

    /// <summary>Gets this sink's position in the deterministic fan-out order.</summary>
    /// <value>Lower values are delivered to first; ties are broken by registration order.</value>
    public int Order { get; }
}
