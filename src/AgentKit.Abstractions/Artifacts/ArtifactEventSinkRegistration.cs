// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Declares one artifact event sink's identity, delivery order, and service lifetime.</summary>
public sealed record ArtifactEventSinkRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="id">The sink's stable identity.</param>
    /// <param name="order">The delivery order; lower values are delivered first and ties break by identity.</param>
    /// <param name="lifetime">The service lifetime the sink instance is resolved under.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is undefined.</exception>
    public ArtifactEventSinkRegistration(ArtifactEventSinkId id, int order, ServiceLifetime lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        Id = id;
        Order = order;
        Lifetime = lifetime;
    }

    /// <summary>Gets the sink's stable identity.</summary>
    public ArtifactEventSinkId Id { get; }

    /// <summary>Gets the delivery order.</summary>
    public int Order { get; }

    /// <summary>Gets the service lifetime the sink instance is resolved under.</summary>
    public ServiceLifetime Lifetime { get; }
}
