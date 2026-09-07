// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>
/// A deterministic, process-local, non-durable <see cref="ISessionStore"/>
/// suitable for tests, examples, and short-lived applications.
/// </summary>
/// <remarks>
/// <para>
/// Every operation is serialized by one store-wide lock. This sacrifices
/// cross-session concurrency in exchange for trivially correct
/// linearizable behavior, which is the right trade-off for a store whose
/// entire purpose is deterministic testing rather than production
/// throughput. All state is lost when the process exits;
/// <see cref="Descriptor"/> reports <c>Durable: false</c> accordingly.
/// </para>
/// <para>
/// Branch sequences are 1-indexed: the first entry ever appended to a
/// branch has sequence 1, and a branch's <see cref="SessionVersion"/> always
/// equals its committed entry count. Branching copies the referenced
/// entries (by value; the copied <see cref="SessionEntry"/> instances keep
/// their original identity and branch provenance) into the new branch's own
/// list, so appends to either branch afterward never affect the other.
/// </para>
/// </remarks>
public sealed partial class InMemorySessionStore: ISessionStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SessionAddress, SessionRecord> _sessions = [];
    private readonly Dictionary<(TenantId TenantId, AgentId AgentId, IdempotencyKey Key), IdempotencyReceipt<SessionCreateRequest, SessionCreated>> _createIdempotency = [];
    private readonly Dictionary<(TenantId TenantId, AgentId AgentId, IdempotencyKey Key), SessionCreateRequest> _deletedCreateIdempotency = [];
    private readonly Dictionary<(TenantId TenantId, SessionAddress Address, IdempotencyKey Key), IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>> _deleteIdempotency = [];
    private readonly IIdentifierGenerator<SessionId> _sessionIds;
    private readonly IIdentifierGenerator<BranchId> _branchIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemorySessionStore> _logger;

    /// <summary>Initializes a new instance of the <see cref="InMemorySessionStore"/> class.</summary>
    /// <param name="sessionIds">Generates the identity of each newly created session.</param>
    /// <param name="branchIds">Generates the identity of each newly created branch.</param>
    /// <param name="timeProvider">The clock used to timestamp created and updated sessions.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public InMemorySessionStore(
        IIdentifierGenerator<SessionId> sessionIds,
        IIdentifierGenerator<BranchId> branchIds,
        TimeProvider timeProvider,
        ILogger<InMemorySessionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        ArgumentNullException.ThrowIfNull(branchIds);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _sessionIds = sessionIds;
        _branchIds = branchIds;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<InMemorySessionStore>.Instance;
    }

    /// <inheritdoc/>
    public SessionStoreDescriptor Descriptor { get; } = new(new SessionStoreKey("agentkit.in-memory"), durable: false);

    /// <inheritdoc/>
    private ValueTask<SessionCreateResult> CreateCoreAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var idempotencyEntry = (request.Identity.TenantId, request.AgentId, request.IdempotencyKey);
            if (_deletedCreateIdempotency.TryGetValue(idempotencyEntry, out var deletedRequest))
            {
                return ValueTask.FromResult<SessionCreateResult>(
                    deletedRequest.Equals(request)
                        ? new SessionCreateFailed("The session created by this idempotency key was deleted.")
                        : new SessionCreateFailed("The idempotency key was previously used with different request evidence."));
            }

            if (_createIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionCreateResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionCreateFailed("The idempotency key was previously used with different request evidence."));
            }

            var now = _timeProvider.GetUtcNow();
            var branchId = _branchIds.Create();
            var address = new SessionAddress(request.AgentId, _sessionIds.Create());
            var record = new SessionRecord(
                address,
                request.ConversationId,
                request.Identity.TenantId,
                request.Identity.PrincipalId,
                branchId,
                now);
            record.Branches[branchId] = new BranchRecord();

            _sessions[address] = record;
            var created = new SessionCreated(ToDescriptor(record), existing: false);
            _createIdempotency[idempotencyEntry] = new IdempotencyReceipt<SessionCreateRequest, SessionCreated>(request, created);

            return ValueTask.FromResult<SessionCreateResult>(created);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionLoadResult> LoadCoreAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = context.ToAddress();
            return !_sessions.TryGetValue(address, out var record)
                ? ValueTask.FromResult<SessionLoadResult>(new SessionNotFound(address))
                : record.TenantId != context.Identity.TenantId
                    ? ValueTask.FromResult<SessionLoadResult>(new SessionNotFound(address))
                    : ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(ToDescriptor(record)));
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionAppendResult> AppendCoreAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (record.TenantId != request.Context.Identity.TenantId)
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (!record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (branch.AppendIdempotency.TryGetValue(request.IdempotencyKey, out var cached))
            {
                return ValueTask.FromResult<SessionAppendResult>(
                    cached.Request.Equals(request)
                        ? cached.Result
                        : new SessionAppendFailed("The idempotency key was previously used with different request evidence."));
            }

            var currentVersion = new SessionVersion(branch.Entries.Count);
            if (currentVersion.Value != request.ExpectedVersion.Value)
            {
                return ValueTask.FromResult<SessionAppendResult>(
                    new SessionAppendConflict(request.ExpectedVersion, currentVersion));
            }

            for (var i = 0; i < request.Entries.Length; i++)
            {
                var expectedSequence = request.ExpectedVersion.Value + i + 1;
                if (request.Entries[i].Sequence.Value != expectedSequence)
                {
                    return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                        $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expectedSequence}."));
                }
            }

            branch.Entries.AddRange(request.Entries);
            record.UpdatedAt = _timeProvider.GetUtcNow();

            var newVersion = new SessionVersion(branch.Entries.Count);
            var appended = new SessionAppended(newVersion, request.Entries);
            branch.AppendIdempotency[request.IdempotencyKey] = new IdempotencyReceipt<SessionAppendRequest, SessionAppended>(request, appended);

            return ValueTask.FromResult<SessionAppendResult>(appended);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionPageResult> ReadCoreAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(address));
            }

            var startIndex = (int) request.FromSequenceExclusive.Value;
            var pageEntries = branch.Entries
                .Skip(startIndex)
                .Take(request.PageSize)
                .ToImmutableArray();
            var throughSequence = pageEntries.IsEmpty
                ? request.FromSequenceExclusive
                : new SessionSequence(startIndex + pageEntries.Length);
            var hasMore = startIndex + pageEntries.Length < branch.Entries.Count;

            return ValueTask.FromResult<SessionPageResult>(new SessionPage(pageEntries, throughSequence, hasMore));
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionBranchResult> CreateBranchCoreAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.ParentBranchId, out var parentBranch))
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence));
            }

            if (record.BranchIdempotency.TryGetValue(request.IdempotencyKey, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionBranchFailed("The idempotency key was previously used with different request evidence."));
            }

            if (request.AtSequence.Value > parentBranch.Entries.Count)
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence));
            }

            var newBranchId = _branchIds.Create();
            var newBranch = new BranchRecord();
            newBranch.Entries.AddRange(parentBranch.Entries.Take((int) request.AtSequence.Value));

            record.Branches[newBranchId] = newBranch;
            var branched = new SessionBranched(newBranchId, request.AtSequence);
            record.BranchIdempotency[request.IdempotencyKey] = new IdempotencyReceipt<SessionBranchRequest, SessionBranched>(request, branched);
            record.UpdatedAt = _timeProvider.GetUtcNow();

            return ValueTask.FromResult<SessionBranchResult>(branched);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionDeleteResult> DeleteCoreAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            var idempotencyEntry = (request.Context.Identity.TenantId, address, request.IdempotencyKey);
            if (_deleteIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionDeleteResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionDeleteFailed("The idempotency key was previously used with different request evidence."));
            }

            if (_sessions.TryGetValue(address, out var record)
                && record.TenantId != request.Context.Identity.TenantId)
            {
                return ValueTask.FromResult<SessionDeleteResult>(new SessionDeleted(address));
            }

            if (_sessions.Remove(address))
            {
                var createReceipt = _createIdempotency
                    .FirstOrDefault(pair => pair.Value.Result.Descriptor.Address == address);
                if (createReceipt.Value is not null)
                {
                    _ = _createIdempotency.Remove(createReceipt.Key);
                    _deletedCreateIdempotency[createReceipt.Key] = createReceipt.Value.Request;
                }
            }

            var deleted = new SessionDeleted(address);
            _deleteIdempotency[idempotencyEntry] = new IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>(request, deleted);
            return ValueTask.FromResult<SessionDeleteResult>(deleted);
        }
    }

    private SessionDescriptor ToDescriptor(SessionRecord record) => new(
        record.Address,
        record.ConversationId,
        record.TenantId,
        record.OwnerId,
        Descriptor.Key,
        record.ActiveBranchId,
        new SessionVersion(record.Branches[record.ActiveBranchId].Entries.Count),
        record.State,
        record.CreatedAt,
        record.UpdatedAt,
        new SchemaVersion("1"),
        ExtensionData.Empty);
}
