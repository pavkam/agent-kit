// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that one operation durably recorded a wait on an external owner, approval, or deferred instant.</summary>
/// <remarks>
/// A deferred operation has released its local caller wait but remains durable work. The event reports whether an
/// external owner reference exists and when the operation becomes eligible again; it never carries approval content or
/// an external credential.
/// </remarks>
public sealed record DurableOperationDeferred: DurableExecutionEvent
{
    /// <summary>Initializes a waiting-record observation.</summary>
    /// <param name="binding">The non-null address and captured execution context of the waiting operation.</param>
    /// <param name="occurredAt">The injected-clock instant at which the waiting record committed.</param>
    /// <param name="sideEffectCertainty">The truthful certainty about the relevant external effect while waiting.</param>
    /// <param name="fencingToken">The ownership generation that committed the waiting record.</param>
    /// <param name="notBefore">The earliest instant at which the operation becomes eligible again, when one applies.</param>
    /// <param name="externalReference">The external durable owner's reference, when the wait was handed off.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sideEffectCertainty"/> is not a defined member, or <paramref name="fencingToken"/> is the
    /// default value.
    /// </exception>
    public DurableOperationDeferred(
        DurableOperationBinding binding,
        DateTimeOffset occurredAt,
        SideEffectCertainty sideEffectCertainty,
        FencingToken fencingToken,
        DateTimeOffset? notBefore = null,
        ExternalOperationReference? externalReference = null)
        : base(binding, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        SideEffectCertainty = sideEffectCertainty;
        FencingToken = fencingToken;
        NotBefore = notBefore;
        ExternalReference = externalReference;
    }

    /// <summary>Gets the truthful external-effect certainty observed while waiting.</summary>
    /// <value>A defined <see cref="SideEffectCertainty"/> member.</value>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <summary>Gets the ownership generation that committed the waiting record.</summary>
    /// <value>The non-default fencing token presented by the waiting worker.</value>
    public FencingToken FencingToken { get; }

    /// <summary>Gets the earliest eligibility instant.</summary>
    /// <value>An injected-clock instant, or null when the wait has no time component.</value>
    public DateTimeOffset? NotBefore { get; }

    /// <summary>Gets the external durable owner's reference.</summary>
    /// <value>A content-free backend handle, or null when no handoff occurred.</value>
    public ExternalOperationReference? ExternalReference { get; }
}
