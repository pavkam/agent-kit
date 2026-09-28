// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableCheckpoint"/>, one durable progress marker for an in-flight operation.</summary>
/// <remarks>
/// Only the latest acknowledged checkpoint is behaviorally meaningful to recovery, but each checkpoint carries its own
/// identity and writer token so a store can prove which writer produced the state it is about to replay. The checkpoint
/// kind is persisted by name under the canonical encoding contract because it is exactly the field a recovery policy
/// branches on.
/// </remarks>
/// <param name="Id">The nonempty checkpoint identity.</param>
/// <param name="Binding">The non-null persisted address and captured durability context.</param>
/// <param name="Kind">The checkpoint's declared progress kind.</param>
/// <param name="State">The non-null versioned state captured at this checkpoint.</param>
/// <param name="FencingToken">The writer token presented when the checkpoint was written.</param>
/// <param name="RecordedAt">The instant the caller's clock reported when the checkpoint was written.</param>
internal sealed record DurableCheckpointDocument(
    Guid Id,
    DurableBindingDocument Binding,
    DurableCheckpointKind Kind,
    OperationPayloadDocument State,
    long FencingToken,
    DateTimeOffset RecordedAt)
{
    /// <summary>Projects one domain checkpoint into its portable persisted representation.</summary>
    /// <param name="value">The non-null checkpoint to project.</param>
    /// <returns>A document carrying every unwrapped checkpoint field.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableCheckpointDocument FromDomain(DurableCheckpoint value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableCheckpointDocument(
            value.Id.Value,
            DurableBindingDocument.FromDomain(value.Binding),
            value.Kind,
            OperationPayloadDocument.FromDomain(value.State),
            value.FencingToken.Value,
            value.RecordedAt);
    }

    /// <summary>Reconstructs the exact domain checkpoint this document was projected from.</summary>
    /// <returns>A checkpoint equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> or <see cref="State"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Id"/> is empty, <see cref="Kind"/> is undefined, or <see cref="FencingToken"/> is not positive.</exception>
    internal DurableCheckpoint ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        ArgumentNullException.ThrowIfNull(State);
        return new DurableCheckpoint(
            new CheckpointId(Id),
            Binding.ToDomain(),
            Kind,
            State.ToDomain(),
            new FencingToken(FencingToken),
            RecordedAt);
    }
}
