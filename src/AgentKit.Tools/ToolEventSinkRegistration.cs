// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Declares one tool-event sink's stable identity and delivery order.</summary>
/// <remarks>
/// Sinks are observational and always singleton: the dispatcher shares one instance across concurrent calls, so a sink
/// implementation must be safe to call concurrently. Delivery is best effort by construction; there is no required mode,
/// because a sink can never influence an outcome.
/// </remarks>
public sealed record ToolEventSinkRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="id">The sink's nonblank stable identity, unique among tool-event sinks.</param>
    /// <param name="order">The delivery order; lower values are delivered first and ties break by ordinal identity.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is blank.</exception>
    public ToolEventSinkRegistration(ComponentId id, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        Id = id;
        Order = order;
    }

    /// <summary>Gets the sink's stable identity.</summary>
    /// <value>A nonblank identity unique among tool-event sinks.</value>
    public ComponentId Id { get; }

    /// <summary>Gets the delivery order.</summary>
    /// <value>Lower values are delivered first.</value>
    public int Order { get; }
}
