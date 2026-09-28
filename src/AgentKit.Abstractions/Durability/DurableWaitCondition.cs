// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The reason a running recoverable operation cannot proceed yet, supplied by
/// a handler to <see cref="IDurableCheckpointWriter.RecordWaitingAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The condition names only what the handler knows. Everything a durable
/// waiting record additionally requires — the binding, the writing lease's
/// fencing generation, and the commit instant — belongs to the coordinator
/// that owns the journal, so a handler cannot claim a generation it does not
/// hold or backdate a wait it just started.
/// </para>
/// <para>
/// At least one wait condition must be present. A "waiting" record that names
/// no owner, no receiver key, and no not-before instant would tell recovery
/// that progress stopped without telling it what would ever let progress
/// resume, which is indistinguishable from a lost operation.
/// </para>
/// </remarks>
public sealed record DurableWaitCondition
{
    /// <summary>Initializes one complete wait condition.</summary>
    /// <param name="sideEffectCertainty">
    /// What is truthfully known about the relevant external effect at the instant waiting begins. An approval wait
    /// that has performed nothing is <see cref="SideEffectCertainty.DefinitelyNotPerformed"/>; a handoff whose
    /// receiver may already be acting is <see cref="SideEffectCertainty.Unknown"/>.
    /// </param>
    /// <param name="externalReference">
    /// The external owner's handle when work was handed off, or <see langword="null"/> when no external owner holds
    /// it.
    /// </param>
    /// <param name="externalIdempotencyKey">
    /// The key the effect owner accepted for duplicate collapse, or <see langword="null"/> when none was
    /// established.
    /// </param>
    /// <param name="notBefore">
    /// The instant before which the operation must not be re-driven, or <see langword="null"/> when no temporal
    /// deferral applies.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sideEffectCertainty"/> is not a defined enumeration value, or every wait condition is
    /// absent.
    /// </exception>
    public DurableWaitCondition(
        SideEffectCertainty sideEffectCertainty,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null,
        DateTimeOffset? notBefore = null)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        if (externalReference is null && externalIdempotencyKey is null && notBefore is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(notBefore),
                notBefore,
                "A wait condition requires an external reference, external idempotency key, or not-before instant.");
        }

        SideEffectCertainty = sideEffectCertainty;
        ExternalReference = externalReference;
        ExternalIdempotencyKey = externalIdempotencyKey;
        NotBefore = notBefore;
    }

    /// <summary>Gets what is known about the external effect when waiting began.</summary>
    /// <value>A defined <see cref="SideEffectCertainty"/> member describing the effect, never the record write.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer supplies an undefined enumeration value.</exception>
    public SideEffectCertainty SideEffectCertainty
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(SideEffectCertainty));
            field = value;
        }
    }

    /// <summary>Gets the external owner's handle, when one holds the work.</summary>
    /// <value>The backend-scoped handle recovery would consult, or <see langword="null"/> when no owner was named.</value>
    public ExternalOperationReference? ExternalReference { get; init; }

    /// <summary>Gets the key the effect owner accepted for duplicate collapse.</summary>
    /// <value>The receiver-honored key, or <see langword="null"/> when the receiver established none.</value>
    public IdempotencyKey? ExternalIdempotencyKey { get; init; }

    /// <summary>Gets the instant before which re-drive must not occur.</summary>
    /// <value>An injected-clock instant, or <see langword="null"/> when waiting is not temporal.</value>
    public DateTimeOffset? NotBefore { get; init; }
}
