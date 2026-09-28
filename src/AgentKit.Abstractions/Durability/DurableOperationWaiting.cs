// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The durable record that an operation is waiting on an external owner,
/// approval, or a deferred not-before instant before it may be driven again.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// A waiting record commits no effect and does not publish a terminal outcome.
/// It makes the deferred condition discoverable after process loss so recovery
/// can resume waiting or re-drive once the condition is satisfied.
/// </para>
/// </remarks>
public sealed record DurableOperationWaiting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableOperationWaiting"/>
    /// record.
    /// </summary>
    /// <param name="binding">The exact durable coordinates and captured context.</param>
    /// <param name="fencingToken">
    /// The ownership generation of the lease committing this record.
    /// </param>
    /// <param name="recordedAt">
    /// The instant the waiting state was committed, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="sideEffectCertainty">
    /// What is actually known about whether the relevant external effect
    /// occurred while waiting began.
    /// </param>
    /// <param name="externalReference">
    /// The external owner's handle when work was handed off, or
    /// <see langword="null"/> when waiting is purely temporal or approval-based.
    /// </param>
    /// <param name="externalIdempotencyKey">
    /// The key accepted by the effect owner, when one was established.
    /// </param>
    /// <param name="notBefore">
    /// The instant before which the operation must not be re-driven, or
    /// <see langword="null"/> when no deferral applies.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fencingToken"/> is default, <paramref name="sideEffectCertainty"/> is undefined, or no wait
    /// condition was supplied.
    /// </exception>
    public DurableOperationWaiting(
        DurableOperationBinding binding,
        FencingToken fencingToken,
        DateTimeOffset recordedAt,
        SideEffectCertainty sideEffectCertainty,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null,
        DateTimeOffset? notBefore = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        if (externalReference is null && externalIdempotencyKey is null && notBefore is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(notBefore),
                notBefore,
                "A waiting record requires an external reference, external idempotency key, or not-before instant.");
        }

        Binding = binding;
        FencingToken = fencingToken;
        RecordedAt = recordedAt;
        SideEffectCertainty = sideEffectCertainty;
        ExternalReference = externalReference;
        ExternalIdempotencyKey = externalIdempotencyKey;
        NotBefore = notBefore;
    }

    /// <summary>Gets the exact durable coordinates and captured context.</summary>
    /// <value>The inseparable address and authorization evidence for this wait.</value>
    public DurableOperationBinding Binding { get; }

    /// <summary>Gets the operation coordinates derived from <see cref="Binding"/>.</summary>
    /// <value>The exact immutable address validated with this wait record.</value>
    public DurableOperationAddress Address => Binding.Address;

    /// <summary>Gets the captured durability context derived from <see cref="Binding"/>.</summary>
    /// <value>The exact immutable selection validated with this wait record.</value>
    public DurableExecutionContext ExecutionContext => Binding.ExecutionContext;

    /// <summary>Gets the ownership generation committing this record.</summary>
    /// <value>A monotonically allocated fence from the active execution lease.</value>
    public FencingToken FencingToken { get; init; }

    /// <summary>Gets when the waiting state was committed.</summary>
    /// <value>An instant from the injected <see cref="TimeProvider"/>.</value>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets what is known about the external effect at wait commit time.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer sets an undefined enumeration value.</exception>
    public SideEffectCertainty SideEffectCertainty
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(SideEffectCertainty));
            field = value;
        }
    }

    /// <summary>Gets the external owner's handle, when one was recorded.</summary>
    public ExternalOperationReference? ExternalReference { get; init; }

    /// <summary>Gets the external idempotency key, when one was recorded.</summary>
    public IdempotencyKey? ExternalIdempotencyKey { get; init; }

    /// <summary>Gets the instant before which re-drive must not occur.</summary>
    public DateTimeOffset? NotBefore { get; init; }
}
