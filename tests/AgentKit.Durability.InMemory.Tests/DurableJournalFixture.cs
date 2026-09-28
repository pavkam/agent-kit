// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Pairs one journal instance with the harness that issues exactly matching grants for it.</summary>
/// <remarks>
/// Grants must match what the journal recomputes, including the journal key, audience, resource, and request digest,
/// so a fixture is the only way to authorize a request without duplicating that derivation in every test. Each
/// <c>Authorized*</c> call mints a fresh single-use grant, which is why repeating a write in a test is a new
/// authorization rather than a replayed one.
/// </remarks>
internal sealed class DurableJournalFixture
{
    /// <summary>Initializes a journal bound to a fresh harness under a caller-chosen clock and logger.</summary>
    /// <param name="timeProvider">The clock the journal reads, defaulting to the system clock.</param>
    /// <param name="logger">The logger the journal writes to, defaulting to none.</param>
    /// <param name="auditRecordIds">The audit-identity generator, defaulting to a deterministic sequence.</param>
    internal DurableJournalFixture(
        TimeProvider? timeProvider = null,
        ILogger<InMemoryDurableOperationJournal>? logger = null,
        IIdentifierGenerator<SecurityAuditRecordId>? auditRecordIds = null) =>
        Journal = new InMemoryDurableOperationJournal(
            DurableJournalTestData.JournalKey,
            auditRecordIds ?? new SequentialAuditRecordIdGenerator(),
            Harness,
            Harness,
            timeProvider ?? TimeProvider.System,
            logger);

    /// <summary>Gets the harness acting as both grant store and audit dispatcher for <see cref="Journal"/>.</summary>
    /// <value>The mutable harness whose knobs let a test deny consumption or audit.</value>
    internal TestDurableSecurityHarness Harness { get; } = new();

    /// <summary>Gets the journal under test.</summary>
    /// <value>A journal whose every operation consumes a grant from <see cref="Harness"/>.</value>
    internal InMemoryDurableOperationJournal Journal { get; }

    /// <summary>Authorizes one acceptance write under a fresh single-use grant.</summary>
    /// <param name="token">The ownership generation the write presents and its intent requires.</param>
    /// <param name="operationId">The operation the write targets, defaulting to the fixture's operation.</param>
    /// <param name="context">The captured durable context, defaulting to the fixture's context.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    internal AuthorizedDurableRequest<DurableOperationStart> AuthorizedStart(
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null)
    {
        var start = DurableJournalTestData.Start(token, operationId, context);
        return AuthorizeWrite(
            start,
            start.Descriptor.Binding,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityEffect.Create,
            token);
    }

    /// <summary>Authorizes one checkpoint write under a fresh single-use grant.</summary>
    /// <param name="token">The ownership generation the write presents and its intent requires.</param>
    /// <param name="operationId">The operation the write targets, defaulting to the fixture's operation.</param>
    /// <param name="context">The captured durable context, defaulting to the fixture's context.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    internal AuthorizedDurableRequest<DurableCheckpoint> AuthorizedCheckpoint(
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null)
    {
        var checkpoint = DurableJournalTestData.Checkpoint(token, operationId, context);
        return AuthorizeWrite(
            checkpoint,
            checkpoint.Binding,
            DurableJournalSecurityBinding.Fingerprint(checkpoint),
            SecurityEffect.Append,
            token);
    }

    /// <summary>Authorizes one terminal write under a fresh single-use grant.</summary>
    /// <param name="token">The ownership generation the write presents and its intent requires.</param>
    /// <param name="operationId">The operation the write targets, defaulting to the fixture's operation.</param>
    /// <param name="context">The captured durable context, defaulting to the fixture's context.</param>
    /// <param name="state">The terminal lifecycle state to record.</param>
    /// <param name="certainty">The truthful side-effect certainty to record.</param>
    /// <param name="marker">Distinguishes payload bytes so two terminal results can differ.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    internal AuthorizedDurableRequest<DurableOperationResult> AuthorizedResult(
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        DurableOperationState state = DurableOperationState.Completed,
        SideEffectCertainty certainty = SideEffectCertainty.DefinitelyPerformed,
        byte marker = 3)
    {
        var result = DurableJournalTestData.Result(token, operationId, context, state, certainty, marker);
        return AuthorizeWrite(
            result,
            result.Binding,
            DurableJournalSecurityBinding.Fingerprint(result),
            SecurityEffect.Append,
            token);
    }

    /// <summary>Authorizes one waiting write under a fresh single-use grant.</summary>
    /// <param name="token">The ownership generation the write presents and its intent requires.</param>
    /// <param name="operationId">The operation the write targets, defaulting to the fixture's operation.</param>
    /// <param name="context">The captured durable context, defaulting to the fixture's context.</param>
    /// <param name="notBefore">The earliest instant the operation may resume.</param>
    /// <param name="certainty">The truthful side-effect certainty while waiting.</param>
    /// <param name="externalReference">The external owner's handle when work was handed off.</param>
    /// <param name="externalIdempotencyKey">The key the effect owner accepted, when one exists.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    internal AuthorizedDurableRequest<DurableOperationWaiting> AuthorizedWaiting(
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        DateTimeOffset? notBefore = null,
        SideEffectCertainty certainty = SideEffectCertainty.Unknown,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null)
    {
        var waiting = DurableJournalTestData.Waiting(
            token, operationId, context, notBefore, certainty, externalReference, externalIdempotencyKey);
        return AuthorizeWrite(
            waiting,
            waiting.Binding,
            DurableJournalSecurityBinding.Fingerprint(waiting),
            SecurityEffect.Mutate,
            token);
    }

    /// <summary>Authorizes one unfenced evidence read under a fresh single-use grant.</summary>
    /// <param name="operationId">The operation to read, defaulting to the fixture's operation.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    internal AuthorizedDurableRequest<DurableOperationAddress> AuthorizedAddress(OperationId? operationId = null)
    {
        var address = DurableJournalTestData.Address(operationId);
        return Harness.Authorize(
            address,
            DurableJournalTestData.JournalKey,
            Journal.SecurityAudience,
            address,
            DurableJournalTestData.Authorization(operationId),
            DurableJournalSecurityBinding.Fingerprint(address),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            requiredFence: null);
    }

    private AuthorizedDurableRequest<TRequest> AuthorizeWrite<TRequest>(
        TRequest request,
        DurableOperationBinding binding,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        FencingToken token)
        where TRequest : class =>
        Harness.Authorize(
            request,
            DurableJournalTestData.JournalKey,
            Journal.SecurityAudience,
            binding.Address,
            binding.ExecutionContext.Authorization,
            fingerprint,
            SecurityOperationKind.StateMutation,
            effect,
            token);

    /// <summary>Produces stable audit-record identities so a test can assert exact audit evidence.</summary>
    private sealed class SequentialAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private int _next;

        public SecurityAuditRecordId Create()
        {
            var value = Interlocked.Increment(ref _next);
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, value);
            bytes[15] = 2;
            return new SecurityAuditRecordId(new Guid(bytes));
        }
    }
}
