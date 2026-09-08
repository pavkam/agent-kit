// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable caller input before preprocessing and durable admission.</summary>
/// <remarks>This value preserves caller-supplied content and delivery intent. It is not admission evidence, an authorization decision, or a queued record.</remarks>
public sealed record AgentInput
{
    /// <summary>Initializes immutable caller input.</summary>
    /// <param name="id">The non-default caller-visible identity used for idempotency.</param>
    /// <param name="delivery">The defined delivery class that distinguishes steering from follow-up work.</param>
    /// <param name="parts">The non-default, nonempty immutable sequence of ordered content parts.</param>
    /// <param name="extensions">The non-null immutable provider-neutral extension data associated with the input.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="delivery"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is default, empty, or contains null.</exception>
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

    /// <summary>Gets the caller-visible identity used to make admission idempotent.</summary>
    /// <value>A non-default input identity; it is preserved when preprocessing creates an effective payload.</value>
    public InputId Id { get; }
    /// <summary>Gets the requested delivery class.</summary>
    /// <value><see cref="InputDelivery.Steer"/> for the next safe boundary or <see cref="InputDelivery.FollowUp"/> for otherwise-idle work.</value>
    public InputDelivery Delivery { get; }
    /// <summary>Gets the caller-supplied content in source order.</summary>
    /// <value>A non-default, nonempty immutable part sequence. The content is not authorization or continuation evidence.</value>
    public ImmutableArray<ContentPart> Parts { get; }
    /// <summary>Gets the provider-neutral extension data supplied with the input.</summary>
    /// <value>A non-null immutable extension set retained with the payload for later policy-controlled handling.</value>
    public ExtensionData Extensions { get; }
}
