// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of one <see cref="DurableOperationProjection"/>, the complete state of one durable operation.</summary>
/// <remarks>
/// A SQLite adapter persists exactly one of these per operation row; a JSON adapter writes one per operation when it
/// compacts its log. Both use the same shape so a store family change cannot alter what recovery observes. The
/// projection is self-describing: it carries the declaration, the latest checkpoint, and the terminal record in full
/// rather than referencing other rows or lines, so a single decoded document answers an evidence load completely.
/// </remarks>
/// <param name="Binding">The non-null coordinates and captured authorization acceptance committed under.</param>
/// <param name="Descriptor">The declaration acceptance committed, or <see langword="null"/> when none was retained.</param>
/// <param name="State">The persisted lifecycle position.</param>
/// <param name="SideEffectCertainty">What is provably known about the external effect.</param>
/// <param name="LatestCheckpoint">The most recent state snapshot, or <see langword="null"/> when none was recorded.</param>
/// <param name="TerminalResult">The settling record, or <see langword="null"/> while the operation is unsettled.</param>
/// <param name="NotBefore">The earliest resumption instant, or <see langword="null"/> when the operation may resume immediately.</param>
/// <param name="ExternalReference">The handle naming awaited external work, or <see langword="null"/> when none was issued.</param>
/// <param name="ExternalIdempotencyKey">The external owner's duplicate-collapsing key, or <see langword="null"/> when none applies.</param>
/// <param name="LastWriterToken">The ownership generation of the last successful durable write.</param>
internal sealed record DurableOperationProjectionDocument(
    DurableBindingDocument Binding,
    RecoverableOperationDescriptorDocument? Descriptor,
    DurableOperationState State,
    SideEffectCertainty SideEffectCertainty,
    DurableCheckpointDocument? LatestCheckpoint,
    DurableOperationResultDocument? TerminalResult,
    DateTimeOffset? NotBefore,
    ExternalOperationReferenceDocument? ExternalReference,
    string? ExternalIdempotencyKey,
    long LastWriterToken)
{
    /// <summary>Projects one live projection into its portable persisted representation.</summary>
    /// <param name="value">The non-null projection to persist.</param>
    /// <returns>A document carrying the operation's complete accumulated state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableOperationProjectionDocument FromDomain(DurableOperationProjection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableOperationProjectionDocument(
            DurableBindingDocument.FromDomain(value.Binding),
            value.Descriptor is { } descriptor
                ? RecoverableOperationDescriptorDocument.FromDomain(descriptor)
                : null,
            value.State,
            value.SideEffectCertainty,
            value.LatestCheckpoint is { } checkpoint
                ? DurableCheckpointDocument.FromDomain(checkpoint)
                : null,
            DurableOperationResultDocument.FromDomain(value.TerminalResult),
            value.NotBefore,
            ExternalOperationReferenceDocument.FromDomain(value.ExternalReference),
            value.ExternalIdempotencyKey?.Value,
            value.LastWriterToken.Value);
    }

    /// <summary>Reconstructs the exact live projection this document was persisted from.</summary>
    /// <returns>A projection whose evidence is indistinguishable from the one that was persisted.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">A nested persisted record is malformed or its authorization no longer describes its address.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted enumeration value is undefined, or a persisted token or key is out of range.</exception>
    internal DurableOperationProjection ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        return new DurableOperationProjection(Binding.ToDomain(), new FencingToken(LastWriterToken))
        {
            Descriptor = Descriptor?.ToDomain(),
            State = State,
            SideEffectCertainty = SideEffectCertainty,
            LatestCheckpoint = LatestCheckpoint?.ToDomain(),
            TerminalResult = TerminalResult?.ToDomain(),
            NotBefore = NotBefore,
            ExternalReference = ExternalReference?.ToDomain(),
            ExternalIdempotencyKey = ExternalIdempotencyKey is { } key ? new IdempotencyKey(key) : null,
        };
    }
}
