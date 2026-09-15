// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>
/// A real, single-branch, in-memory <see cref="ISessionCoordinator"/> test
/// double supporting exactly the operations <see cref="DefaultCompactor"/>
/// uses — <see cref="ReadAsync"/> and <see cref="AppendAsync"/> — so
/// compaction behavior can be tested against real paging and
/// optimistic-concurrency semantics without a full session store.
/// </summary>
/// <remarks>
/// Session lifecycle operations unrelated to compaction
/// (<see cref="CreateAsync"/>, <see cref="LoadAsync"/>,
/// <see cref="BranchAsync"/>, <see cref="DeleteAsync"/>) are intentionally
/// unsupported by this fake and throw if called, since no compactor
/// behavior under test invokes them.
/// </remarks>
internal sealed class FakeSessionCoordinator: ISessionCoordinator
{
    private readonly List<SessionEntry> _entries = [];
    private readonly List<SessionAppendRequest> _receivedAppends = [];
    private readonly List<SessionReadRequest> _receivedReads = [];
    private readonly HashSet<SessionReadSnapshot> _issuedSnapshots = [];

    /// <summary>Initializes a new instance of the <see cref="FakeSessionCoordinator"/> class.</summary>
    /// <param name="branchId">The single branch this fake serves.</param>
    public FakeSessionCoordinator(BranchId branchId) => BranchId = branchId;

    /// <summary>Gets the single branch this fake serves.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the branch's current version.</summary>
    public SessionVersion Version { get; private set; }

    /// <summary>Gets the sequence of the last committed entry, or zero for an empty branch.</summary>
    public SessionSequence TipSequence => _entries.Count == 0 ? new SessionSequence(0) : _entries[^1].Sequence;

    /// <summary>Gets every entry currently committed to the branch, in sequence order.</summary>
    public IReadOnlyList<SessionEntry> Entries => _entries;

    /// <summary>Gets every append request this fake has received, in call order.</summary>
    public IReadOnlyList<SessionAppendRequest> ReceivedAppends => _receivedAppends;

    /// <summary>Gets every read request this fake has received, in call order.</summary>
    public IReadOnlyList<SessionReadRequest> ReceivedReads => _receivedReads;

    /// <summary>
    /// Gets or sets a callback invoked before each read is served, letting tests
    /// mutate the branch between pages of one paged read.
    /// </summary>
    public Action<SessionReadRequest>? OnRead { get; set; }

    /// <summary>
    /// Gets or sets an override invoked instead of the normal append
    /// behavior, letting tests script a specific failure outcome.
    /// </summary>
    public Func<SessionAppendRequest, SessionAppendResult>? AppendOverride { get; set; }

    /// <summary>
    /// Gets or sets an override invoked instead of the normal read
    /// behavior, letting tests script a specific failure outcome.
    /// </summary>
    public Func<SessionReadRequest, SessionPageResult>? ReadOverride { get; set; }

    /// <summary>Seeds the branch with entries as if they had already been committed.</summary>
    /// <param name="entries">The entries to seed, in commit order.</param>
    public void Seed(IEnumerable<SessionEntry> entries)
    {
        foreach (var entry in entries)
        {
            _entries.Add(entry);
        }

        Version = new SessionVersion(_entries.Count);
    }

    /// <summary>
    /// Overrides the branch version independently of the entry count, modelling
    /// a store where several entries were committed in one append.
    /// </summary>
    /// <param name="version">The version to report.</param>
    public void SetVersion(SessionVersion version) => Version = version;

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _receivedAppends.Add(request);

        if (AppendOverride is not null)
        {
            return ValueTask.FromResult(AppendOverride(request));
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

        // Mirror InMemorySessionStore: every appended entry must carry the next contiguous sequence after the tip.
        var tip = TipSequence.Value;
        for (var i = 0; i < request.Entries.Length; i++)
        {
            var expectedSequence = tip + i + 1;
            if (request.Entries[i].Sequence.Value != expectedSequence)
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                    $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expectedSequence}."));
            }
        }

        _entries.AddRange(request.Entries);
        Version = new SessionVersion(Version.Value + 1);

        return ValueTask.FromResult<SessionAppendResult>(new SessionAppended(Version, request.Entries));
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _receivedReads.Add(request);
        OnRead?.Invoke(request);

        if (ReadOverride is not null)
        {
            return ValueTask.FromResult(ReadOverride(request));
        }

        if (request.BranchId != BranchId)
        {
            return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(request.Context.ToAddress()));
        }

        // Mirror InMemorySessionStore: a continuation must present a snapshot this fake issued, and every page is
        // pinned to that snapshot's upper sequence so appends after the first page never leak into later pages.
        if (request.Snapshot is { } supplied && !_issuedSnapshots.Contains(supplied))
        {
            return ValueTask.FromResult<SessionPageResult>(
                new SessionReadFailed("The supplied session read snapshot is not available for this branch."));
        }

        var snapshot = request.Snapshot
            ?? new SessionReadSnapshot(request.Context.ToAddress(), BranchId, Version, TipSequence);
        if (request.Snapshot is null)
        {
            _ = _issuedSnapshots.Add(snapshot);
        }

        var page = _entries
            .Where(e => e.Sequence.Value > request.FromSequenceExclusive.Value && e.Sequence.Value <= snapshot.UpperSequence.Value)
            .Take(request.PageSize)
            .ToImmutableArray();
        var throughSequence = page.IsEmpty ? request.FromSequenceExclusive : page[^1].Sequence;
        var hasMore = _entries.Any(e => e.Sequence.Value > throughSequence.Value && e.Sequence.Value <= snapshot.UpperSequence.Value);

        return ValueTask.FromResult<SessionPageResult>(new SessionPage(page, throughSequence, hasMore, snapshot));
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support session creation.");

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support session loading.");

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support branching.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support deletion.");
}
