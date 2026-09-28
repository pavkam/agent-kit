// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Decides and applies the lifecycle, fencing, and conflict rules every durable journal adapter shares.</summary>
/// <remarks>
/// <para>
/// Each write is split into a pure rejection decision and an application step. A store calls the rejection first, and
/// only persists when it returns <see langword="null"/>, so a refused write can never leave partial state behind.
/// The application step is deliberately free of clock access: a store reads its clock before applying, so a failing
/// clock cannot strand a write that already committed outside the <see cref="DurableRecordFailed"/> channel.
/// </para>
/// <para>
/// Keeping these rules here rather than in each leaf is what makes the SQLite and JSON adapters answer the shared
/// conformance suite identically. Fencing compares the presented generation against the last committed writer:
/// a strictly older generation is fenced, an equal or newer one proceeds, and settlement is permanent regardless of
/// generation.
/// </para>
/// </remarks>
internal static class DurableJournalTransition
{
    /// <summary>Determines whether a lifecycle state already carries a terminal record.</summary>
    /// <param name="state">The persisted lifecycle position to classify.</param>
    /// <returns><see langword="true"/> when the operation has settled and accepts no further progress write.</returns>
    internal static bool IsTerminal(DurableOperationState state) => state is
        DurableOperationState.OutcomeReady or DurableOperationState.Completed or DurableOperationState.Faulted;

    /// <summary>Decides whether one acceptance record must be refused before anything is persisted.</summary>
    /// <param name="existing">The currently persisted projection, or <see langword="null"/> when the address is free.</param>
    /// <param name="start">The non-null acceptance declaration presented by the writer.</param>
    /// <returns>The terminal refusal, or <see langword="null"/> when the write may be applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
    /// <remarks>
    /// Re-recording acceptance for an operation still in <see cref="DurableOperationState.Accepted"/> is permitted so a
    /// retried admission is not spuriously refused, but an operation that already progressed cannot be restarted: the
    /// refusal reports <c>committed: true</c> because the store's existing state remains valid and unchanged.
    /// </remarks>
    internal static DurableRecordResult? RejectStart(
        DurableOperationProjection? existing,
        DurableOperationStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return existing switch
        {
            null => null,
            _ when start.FencingToken.Value < existing.LastWriterToken.Value =>
                new DurableRecordFenced(start.FencingToken, existing.LastWriterToken),
            _ when existing.Binding != start.Descriptor.Binding =>
                new DurableRecordFailed(
                    "A different execution context is already recorded for this operation.", committed: false),
            _ when existing.State != DurableOperationState.Accepted =>
                new DurableRecordFailed(
                    "This operation has already progressed past acceptance and cannot be restarted.", committed: true),
            _ => null,
        };
    }

    /// <summary>Applies one accepted acceptance record to the projection a store is about to persist.</summary>
    /// <param name="existing">The currently persisted projection, or <see langword="null"/> when the address is free.</param>
    /// <param name="start">The non-null acceptance declaration <see cref="RejectStart"/> already permitted.</param>
    /// <returns>The projection the store must persist, which is <paramref name="existing"/> when one was supplied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
    internal static DurableOperationProjection ApplyStart(
        DurableOperationProjection? existing,
        DurableOperationStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var projection = existing ?? new DurableOperationProjection(start.Descriptor.Binding, start.FencingToken);
        projection.LastWriterToken = start.FencingToken;
        projection.Descriptor = start.Descriptor;
        return projection;
    }

    /// <summary>Decides whether one state snapshot must be refused before anything is persisted.</summary>
    /// <param name="existing">The currently persisted projection, or <see langword="null"/> when the address is unknown.</param>
    /// <param name="checkpoint">The non-null snapshot presented by the writer.</param>
    /// <returns>The terminal refusal, or <see langword="null"/> when the write may be applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is null.</exception>
    internal static DurableRecordResult? RejectCheckpoint(
        DurableOperationProjection? existing,
        DurableCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return existing switch
        {
            null => new DurableRecordFailed("No accepted record exists for this operation.", committed: false),
            _ when checkpoint.FencingToken.Value < existing.LastWriterToken.Value =>
                new DurableRecordFenced(checkpoint.FencingToken, existing.LastWriterToken),
            _ when existing.Binding != checkpoint.Binding =>
                new DurableRecordFailed(
                    "A different execution context is already recorded for this operation.", committed: false),
            _ when IsTerminal(existing.State) =>
                new DurableRecordFailed(
                    "This operation already has a terminal record; a further checkpoint cannot be recorded.",
                    committed: true),
            _ => null,
        };
    }

    /// <summary>Applies one accepted state snapshot to the projection a store is about to persist.</summary>
    /// <param name="existing">The non-null projection <see cref="RejectCheckpoint"/> already permitted the write against.</param>
    /// <param name="checkpoint">The non-null snapshot to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> or <paramref name="checkpoint"/> is null.</exception>
    /// <remarks>
    /// A checkpoint moves the operation to <see cref="DurableOperationState.EffectPending"/> with
    /// <see cref="SideEffectCertainty.Unknown"/>, because progress past acceptance is exactly the point at which the
    /// store can no longer prove the external effect did not happen.
    /// </remarks>
    internal static void ApplyCheckpoint(DurableOperationProjection existing, DurableCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(checkpoint);
        existing.State = DurableOperationState.EffectPending;
        existing.SideEffectCertainty = SideEffectCertainty.Unknown;
        existing.LatestCheckpoint = checkpoint;
        existing.LastWriterToken = checkpoint.FencingToken;
    }

    /// <summary>Decides whether one terminal record must be refused before anything is persisted.</summary>
    /// <param name="existing">The currently persisted projection, or <see langword="null"/> when the address is unknown.</param>
    /// <param name="terminal">The non-null terminal record presented by the writer.</param>
    /// <returns>The terminal refusal, or <see langword="null"/> when the write may be applied or is an exact repeat.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="terminal"/> is null.</exception>
    /// <remarks>
    /// Re-recording the byte-identical terminal result is idempotent and permitted, so a writer that crashed between
    /// committing settlement and observing the acknowledgement can safely retry. A <em>different</em> terminal result
    /// is refused with <c>committed: true</c>, because the first settlement stands.
    /// </remarks>
    internal static DurableRecordResult? RejectTerminal(
        DurableOperationProjection? existing,
        DurableOperationResult terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        return existing switch
        {
            null => new DurableRecordFailed("No accepted record exists for this operation.", committed: false),
            _ when terminal.FencingToken.Value < existing.LastWriterToken.Value =>
                new DurableRecordFenced(terminal.FencingToken, existing.LastWriterToken),
            _ when existing.Binding != terminal.Binding =>
                new DurableRecordFailed(
                    "A different execution context is already recorded for this operation.", committed: false),
            _ when existing.TerminalResult is { } recorded && recorded != terminal =>
                new DurableRecordFailed(
                    "A different terminal result is already recorded for this operation.", committed: true),
            _ => null,
        };
    }

    /// <summary>Determines whether an accepted terminal write repeats the settlement already persisted.</summary>
    /// <param name="existing">The non-null projection the write targets.</param>
    /// <param name="terminal">The non-null terminal record the writer presented.</param>
    /// <returns><see langword="true"/> when the identical result is already retained and nothing needs persisting.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> or <paramref name="terminal"/> is null.</exception>
    internal static bool RepeatsTerminal(DurableOperationProjection existing, DurableOperationResult terminal)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(terminal);
        return existing.TerminalResult == terminal;
    }

    /// <summary>Applies one accepted terminal record to the projection a store is about to persist.</summary>
    /// <param name="existing">The non-null projection <see cref="RejectTerminal"/> already permitted the write against.</param>
    /// <param name="terminal">The non-null terminal record to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> or <paramref name="terminal"/> is null.</exception>
    internal static void ApplyTerminal(DurableOperationProjection existing, DurableOperationResult terminal)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(terminal);
        existing.State = terminal.State;
        existing.SideEffectCertainty = terminal.SideEffectCertainty;
        existing.TerminalResult = terminal;
        existing.LastWriterToken = terminal.FencingToken;
    }

    /// <summary>Decides whether one waiting record must be refused before anything is persisted.</summary>
    /// <param name="existing">The currently persisted projection, or <see langword="null"/> when the address is unknown.</param>
    /// <param name="waiting">The non-null waiting record presented by the writer.</param>
    /// <returns>The terminal refusal, or <see langword="null"/> when the write may be applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="waiting"/> is null.</exception>
    internal static DurableRecordResult? RejectWaiting(
        DurableOperationProjection? existing,
        DurableOperationWaiting waiting)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        return existing switch
        {
            null => new DurableRecordFailed("No accepted record exists for this operation.", committed: false),
            _ when waiting.FencingToken.Value < existing.LastWriterToken.Value =>
                new DurableRecordFenced(waiting.FencingToken, existing.LastWriterToken),
            _ when existing.Binding != waiting.Binding =>
                new DurableRecordFailed(
                    "A different execution context is already recorded for this operation.", committed: false),
            _ when IsTerminal(existing.State) =>
                new DurableRecordFailed(
                    "This operation already has a terminal record; waiting cannot be recorded.", committed: true),
            _ => null,
        };
    }

    /// <summary>Applies one accepted waiting record to the projection a store is about to persist.</summary>
    /// <param name="existing">The non-null projection <see cref="RejectWaiting"/> already permitted the write against.</param>
    /// <param name="waiting">The non-null waiting record to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> or <paramref name="waiting"/> is null.</exception>
    /// <remarks>
    /// The writer's own side-effect certainty is adopted verbatim rather than downgraded, because only the component
    /// that handed the work off knows whether the external owner accepted it.
    /// </remarks>
    internal static void ApplyWaiting(DurableOperationProjection existing, DurableOperationWaiting waiting)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(waiting);
        existing.State = DurableOperationState.Waiting;
        existing.SideEffectCertainty = waiting.SideEffectCertainty;
        existing.NotBefore = waiting.NotBefore;
        existing.ExternalReference = waiting.ExternalReference;
        existing.ExternalIdempotencyKey = waiting.ExternalIdempotencyKey;
        existing.LastWriterToken = waiting.FencingToken;
    }
}
