// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableOperationWaiting"/>, the record parking an operation on an external outcome.</summary>
/// <remarks>
/// Waiting is not settlement. The document preserves the external reference and idempotency key precisely so a
/// recovering worker can reconcile against the external owner instead of guessing, and preserves the earliest
/// resumption instant so a wait survives process loss without collapsing into an immediate retry.
/// </remarks>
/// <param name="Binding">The non-null persisted address and captured durability context.</param>
/// <param name="FencingToken">The writer token presented when the wait was recorded.</param>
/// <param name="RecordedAt">The instant the caller's clock reported when the wait was recorded.</param>
/// <param name="SideEffectCertainty">What is provably known about the operation's external effect while it waits.</param>
/// <param name="ExternalReference">The handle naming the awaited external work, or <see langword="null"/> when none was issued.</param>
/// <param name="ExternalIdempotencyKey">The key the external owner collapses duplicate attempts on, or <see langword="null"/> when none applies.</param>
/// <param name="NotBefore">The earliest instant a worker may resume the operation, or <see langword="null"/> when it may resume immediately.</param>
internal sealed record DurableOperationWaitingDocument(
    DurableBindingDocument Binding,
    long FencingToken,
    DateTimeOffset RecordedAt,
    SideEffectCertainty SideEffectCertainty,
    ExternalOperationReferenceDocument? ExternalReference,
    string? ExternalIdempotencyKey,
    DateTimeOffset? NotBefore)
{
    /// <summary>Projects one domain wait record into its portable persisted representation.</summary>
    /// <param name="value">The non-null wait record to project.</param>
    /// <returns>A document carrying every unwrapped wait field.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableOperationWaitingDocument FromDomain(DurableOperationWaiting value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableOperationWaitingDocument(
            DurableBindingDocument.FromDomain(value.Binding),
            value.FencingToken.Value,
            value.RecordedAt,
            value.SideEffectCertainty,
            ExternalOperationReferenceDocument.FromDomain(value.ExternalReference),
            value.ExternalIdempotencyKey?.Value,
            value.NotBefore);
    }

    /// <summary>Reconstructs the exact domain wait record this document was projected from.</summary>
    /// <returns>A wait record equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SideEffectCertainty"/> is undefined, <see cref="FencingToken"/> is not positive, or a supplied idempotency key is blank.</exception>
    internal DurableOperationWaiting ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        return new DurableOperationWaiting(
            Binding.ToDomain(),
            new FencingToken(FencingToken),
            RecordedAt,
            SideEffectCertainty,
            ExternalReference?.ToDomain(),
            ExternalIdempotencyKey is { } key ? new IdempotencyKey(key) : null,
            NotBefore);
    }
}
