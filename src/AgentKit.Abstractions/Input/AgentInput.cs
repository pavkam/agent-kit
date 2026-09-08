// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable caller input before durable admission.</summary>
public sealed record AgentInput
{
    /// <summary>Initializes caller input.</summary>
    /// <param name="id">The nondefault idempotency identity.</param><param name="delivery">The delivery class.</param>
    /// <param name="parts">The nonempty immutable content.</param><param name="extensions">The nonnull extension data.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="parts"/> is default or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public AgentInput(InputId id, InputDelivery delivery, ImmutableArray<ContentPart> parts, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentException.ThrowIfDefaultOrEmpty(parts);
        ArgumentException.ThrowIfContainsNull(parts);
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        Delivery = delivery;
        Parts = parts;
        Extensions = extensions;
    }

    /// <summary>Gets idempotency identity.</summary><value>A nondefault identity.</value>
    public InputId Id { get; }
    /// <summary>Gets delivery class.</summary><value>The captured class.</value>
    public InputDelivery Delivery { get; }
    /// <summary>Gets ordered content.</summary><value>Nonempty immutable parts.</value>
    public ImmutableArray<ContentPart> Parts { get; }
    /// <summary>Gets extension data.</summary><value>Immutable provider-neutral extensions.</value>
    public ExtensionData Extensions { get; }
}
