// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Runs the reusable journal contract suite against the process-local in-memory adapter.</summary>
/// <remarks>
/// The adapter is explicitly ephemeral, so it declares no durability and the suite skips its reopen case rather than
/// claiming a guarantee this journal cannot provide.
/// </remarks>
public sealed class InMemoryDurableOperationJournalConformanceFixture: IDurableOperationJournalConformanceFixture
{
    private readonly TestDurableSecurityHarness _harness = new();
    private readonly InMemoryDurableOperationJournal _journal;

    /// <summary>Initializes a journal whose grants this fixture mints to match exactly.</summary>
    public InMemoryDurableOperationJournalConformanceFixture() =>
        _journal = new InMemoryDurableOperationJournal(
            JournalKey,
            new ConformanceAuditRecordIdGenerator(),
            _harness,
            _harness,
            new FakeTimeProvider(DurabilityConformanceData.Now));

    /// <inheritdoc/>
    public DurableJournalKey JournalKey { get; } = new("conformance-journal");

    /// <inheritdoc/>
    /// <remarks>Records live only in this process's memory, so acknowledged state does not survive disposal.</remarks>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public IDurableOperationJournal CreateJournal() => _journal;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always. This adapter keeps no storage to reopen.</exception>
    public IDurableOperationJournal Reopen() =>
        throw new NotSupportedException("The in-memory durable journal is ephemeral and cannot be reopened.");

    /// <inheritdoc/>
    public AuthorizedDurableRequest<DurableOperationStart> Authorize(DurableOperationStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return AuthorizeWrite(
            start,
            start.Descriptor.Binding,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityEffect.Create,
            start.FencingToken);
    }

    /// <inheritdoc/>
    public AuthorizedDurableRequest<DurableCheckpoint> Authorize(DurableCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return AuthorizeWrite(
            checkpoint,
            checkpoint.Binding,
            DurableJournalSecurityBinding.Fingerprint(checkpoint),
            SecurityEffect.Append,
            checkpoint.FencingToken);
    }

    /// <inheritdoc/>
    public AuthorizedDurableRequest<DurableOperationResult> Authorize(DurableOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return AuthorizeWrite(
            result,
            result.Binding,
            DurableJournalSecurityBinding.Fingerprint(result),
            SecurityEffect.Append,
            result.FencingToken);
    }

    /// <inheritdoc/>
    public AuthorizedDurableRequest<DurableOperationWaiting> Authorize(DurableOperationWaiting waiting)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        return AuthorizeWrite(
            waiting,
            waiting.Binding,
            DurableJournalSecurityBinding.Fingerprint(waiting),
            SecurityEffect.Mutate,
            waiting.FencingToken);
    }

    /// <inheritdoc/>
    public AuthorizedDurableRequest<DurableOperationAddress> Authorize(DurableOperationAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return _harness.Authorize(
            address,
            JournalKey,
            _journal.SecurityAudience,
            address,
            DurabilityConformanceData.Authorization(address.OperationId),
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
        _harness.Authorize(
            request,
            JournalKey,
            _journal.SecurityAudience,
            binding.Address,
            binding.ExecutionContext.Authorization,
            fingerprint,
            SecurityOperationKind.StateMutation,
            effect,
            token);

    /// <summary>Produces distinct audit-record identities for the conformance journal.</summary>
    private sealed class ConformanceAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }
}
