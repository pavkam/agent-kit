// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// A real, single-branch, in-memory <see cref="ISessionCoordinator"/> test
/// double supporting exactly the operations <see cref="DefaultAgentLoop"/>
/// uses — <see cref="ReadAsync"/> and <see cref="AppendAsync"/> — so loop
/// behavior can be tested against real paging and optimistic-concurrency
/// semantics without a full session store.
/// </summary>
/// <remarks>
/// Session lifecycle operations unrelated to the loop
/// (<see cref="CreateAsync"/>, <see cref="LoadAsync"/>,
/// <see cref="BranchAsync"/>, <see cref="DeleteAsync"/>) are intentionally
/// unsupported by this fake and throw if called, since no loop behavior
/// under test invokes them.
/// </remarks>
internal sealed class FakeSessionCoordinator: ISessionCoordinator
{
    private readonly List<SessionEntry> _entries = [];
    private readonly List<SessionAppendRequest> _receivedAppends = [];

    /// <summary>Initializes a new instance of the <see cref="FakeSessionCoordinator"/> class.</summary>
    /// <param name="branchId">The single branch this fake serves.</param>
    public FakeSessionCoordinator(BranchId branchId) => BranchId = branchId;

    /// <summary>Gets the single branch this fake serves.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the branch's current version.</summary>
    public SessionVersion Version { get; private set; }

    /// <summary>Gets every entry currently committed to the branch, in commit order.</summary>
    public IReadOnlyList<SessionEntry> Entries => _entries;

    /// <summary>Gets every append request this fake has received, in call order.</summary>
    public IReadOnlyList<SessionAppendRequest> ReceivedAppends => _receivedAppends;

    /// <summary>
    /// Gets or sets an override invoked instead of the normal append
    /// behavior, letting tests script a specific failure outcome.
    /// </summary>
    public Func<SessionAppendRequest, SessionAppendResult>? AppendOverride { get; set; }

    /// <summary>
    /// Gets or sets an override consulted before the normal append
    /// behavior. Returning <see langword="null"/> falls through to normal
    /// append logic, letting a test fail only a specific call in a
    /// multi-append sequence (for example, the second of two turn
    /// appends) while every other call commits normally.
    /// </summary>
    public Func<SessionAppendRequest, SessionAppendResult?>? ConditionalAppendOverride { get; set; }

    /// <summary>
    /// Gets or sets an override invoked instead of the normal read
    /// behavior, letting tests script a specific failure outcome.
    /// </summary>
    public Func<SessionReadRequest, SessionPageResult>? ReadOverride { get; set; }

    /// <summary>
    /// Gets or sets an override consulted before the normal read behavior.
    /// Returning <see langword="null"/> falls through to normal read logic,
    /// letting a test fail only a specific read (for example, a rebase
    /// read after an append conflict) while history loading works normally.
    /// </summary>
    public Func<SessionReadRequest, SessionPageResult?>? ConditionalReadOverride { get; set; }

    /// <summary>Gets or sets the conversation identity returned by session descriptor loads.</summary>
    public ConversationId? ConversationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether appended entries must carry the
    /// exact next whole-session sequence numbers, as both first-party stores
    /// require (<c>NextSequence + i + 1</c>). Off by default so legacy tests
    /// that only reason about versions keep working.
    /// </summary>
    public bool EnforceSequenceContinuity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether append and read honour an
    /// already-cancelled token by throwing, as the real
    /// <c>DefaultSessionCoordinator</c> does. Off by default.
    /// </summary>
    public bool HonorCancellation { get; set; }

    /// <summary>Gets the whole-session sequence of the last committed entry.</summary>
    public long NextSequence { get; private set; }

    /// <summary>Seeds the branch with entries as if they had already been committed.</summary>
    /// <param name="entries">The entries to seed, in commit order.</param>
    public void Seed(IEnumerable<SessionEntry> entries)
    {
        foreach (var entry in entries)
        {
            _entries.Add(entry);
            NextSequence = entry.Sequence.Value;
        }

        Version = new SessionVersion(Version.Value + 1);
    }

    /// <summary>
    /// Commits entries as a concurrent writer would, in one version bump:
    /// the version advances by exactly one while the sequence advances by
    /// the number of entries, mirroring the real store semantics.
    /// </summary>
    /// <param name="entries">The entries the concurrent writer committed.</param>
    public void SimulateConcurrentAppend(IEnumerable<SessionEntry> entries)
    {
        foreach (var entry in entries)
        {
            _entries.Add(entry);
            NextSequence = entry.Sequence.Value;
        }

        Version = new SessionVersion(Version.Value + 1);
    }

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (HonorCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        _receivedAppends.Add(request);

        if (AppendOverride is not null)
        {
            return ValueTask.FromResult(AppendOverride(request));
        }

        if (ConditionalAppendOverride?.Invoke(request) is { } conditionalResult)
        {
            return ValueTask.FromResult(conditionalResult);
        }

        if (request.BranchId != BranchId)
        {
            return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(request.Context.ToAddress()));
        }

        if (request.ExpectedVersion.Value != Version.Value)
        {
            return ValueTask.FromResult<SessionAppendResult>(
                new SessionAppendConflict(request.ExpectedVersion, Version));
        }

        if (EnforceSequenceContinuity)
        {
            for (var i = 0; i < request.Entries.Length; i++)
            {
                var expected = NextSequence + i + 1;
                if (request.Entries[i].Sequence.Value != expected)
                {
                    return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                        $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expected}."));
                }
            }
        }

        _entries.AddRange(request.Entries);
        NextSequence += request.Entries.Length;
        Version = new SessionVersion(Version.Value + 1);

        return ValueTask.FromResult<SessionAppendResult>(new SessionAppended(Version, request.Entries));
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (HonorCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (ReadOverride is not null)
        {
            return ValueTask.FromResult(ReadOverride(request));
        }

        if (ConditionalReadOverride?.Invoke(request) is { } conditionalResult)
        {
            return ValueTask.FromResult(conditionalResult);
        }

        if (request.BranchId != BranchId)
        {
            return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(request.Context.ToAddress()));
        }

        var start = (int) request.FromSequenceExclusive.Value;
        var snapshot = request.Snapshot ?? new SessionReadSnapshot(
            request.Context.ToAddress(),
            request.BranchId,
            Version,
            new SessionSequence(_entries.Count));
        var page = _entries
            .Skip(start)
            .TakeWhile(entry => entry.Sequence.Value <= snapshot.UpperSequence.Value)
            .Take(request.PageSize)
            .ToImmutableArray();
        var throughSequence = page.IsEmpty ? request.FromSequenceExclusive : new SessionSequence(start + page.Length);
        var hasMore = throughSequence.Value < snapshot.UpperSequence.Value;

        return ValueTask.FromResult<SessionPageResult>(new SessionPage(page, throughSequence, hasMore, snapshot));
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support session creation.");

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(new SessionDescriptor(
            context.ToAddress(),
            ConversationId,
            context.Identity.TenantId,
            context.Identity.PrincipalId,
            profile.DefaultStoreKey,
            BranchId,
            Version,
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty)));
    }

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support branching.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support deletion.");
}
