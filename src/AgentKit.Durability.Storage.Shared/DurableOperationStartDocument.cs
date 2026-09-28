// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableOperationStart"/>, the admission record committed before any effect runs.</summary>
/// <remarks>
/// The start record is what makes <see cref="RecoveryEvidence.StartDefinitelyAbsent"/> answerable. Once this document
/// is acknowledged by the store, a recovering worker may no longer assume the operation never began, so the document
/// carries the entire declaration rather than a reference to one, and remains readable without any other record.
/// </remarks>
/// <param name="Descriptor">The non-null persisted declaration admitted for this operation.</param>
/// <param name="InitialState">The non-null versioned state the operation was admitted with.</param>
/// <param name="FencingToken">The writer token presented at admission.</param>
/// <param name="AcceptedAt">The instant the caller's clock reported at admission.</param>
internal sealed record DurableOperationStartDocument(
    RecoverableOperationDescriptorDocument Descriptor,
    OperationPayloadDocument InitialState,
    long FencingToken,
    DateTimeOffset AcceptedAt)
{
    /// <summary>Projects one domain admission record into its portable persisted representation.</summary>
    /// <param name="value">The non-null admission record to project.</param>
    /// <returns>A document carrying the projected declaration, state, token, and instant.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableOperationStartDocument FromDomain(DurableOperationStart value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableOperationStartDocument(
            RecoverableOperationDescriptorDocument.FromDomain(value.Descriptor),
            OperationPayloadDocument.FromDomain(value.InitialState),
            value.FencingToken.Value,
            value.AcceptedAt);
    }

    /// <summary>Reconstructs the exact domain admission record this document was projected from.</summary>
    /// <returns>An admission record equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Descriptor"/> or <see cref="InitialState"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="FencingToken"/> is not positive.</exception>
    internal DurableOperationStart ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Descriptor);
        ArgumentNullException.ThrowIfNull(InitialState);
        return new DurableOperationStart(
            Descriptor.ToDomain(),
            InitialState.ToDomain(),
            new FencingToken(FencingToken),
            AcceptedAt);
    }
}
