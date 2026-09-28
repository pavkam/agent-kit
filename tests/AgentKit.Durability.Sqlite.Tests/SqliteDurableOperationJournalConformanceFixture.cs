// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Runs the reusable journal contract suite against the host-local SQLite adapter.</summary>
/// <remarks>
/// The adapter is durable, so the fixture declares durability and <see cref="Reopen"/> returns a second journal over
/// the same database file. Both journals share this fixture's security harness, because a reopened store must accept
/// grants minted for the same audience and key.
/// </remarks>
public sealed class SqliteDurableOperationJournalConformanceFixture: IDurableOperationJournalConformanceFixture
{
    private readonly SqliteDurableTestStore _store = new();
    private readonly TestDurableSecurityHarness _harness = new();
    private readonly SqliteDurableOperationJournal _journal;

    /// <summary>Initializes an empty durable store and one journal over it.</summary>
    public SqliteDurableOperationJournalConformanceFixture() => _journal = CreateInitialized();

    /// <inheritdoc/>
    public DurableJournalKey JournalKey { get; } = new("conformance-journal");

    /// <inheritdoc/>
    /// <remarks>Records are committed to a local database file, so acknowledged state survives disposal.</remarks>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public IDurableOperationJournal CreateJournal() => _journal;

    /// <inheritdoc/>
    public IDurableOperationJournal Reopen() => CreateInitialized();

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

    private SqliteDurableOperationJournal CreateInitialized()
    {
        var journal = new SqliteDurableOperationJournal(
            JournalKey,
            _store.CreateDatabase(),
            new ConformanceAuditRecordIdGenerator(),
            _harness,
            _harness,
            new FakeTimeProvider(DurabilityConformanceData.Now));
        journal.InitializeAsync().AsTask().GetAwaiter().GetResult();
        return journal;
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
