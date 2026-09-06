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

    /// <summary>Initializes a new instance of the <see cref="FakeSessionCoordinator"/> class.</summary>
    /// <param name="branchId">The single branch this fake serves.</param>
    public FakeSessionCoordinator(BranchId branchId) => BranchId = branchId;

    /// <summary>Gets the single branch this fake serves.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the branch's current version.</summary>
    public SessionVersion Version { get; private set; }

    /// <summary>Gets every append request this fake has received, in call order.</summary>
    public IReadOnlyList<SessionAppendRequest> ReceivedAppends => _receivedAppends;

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

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request, CancellationToken cancellationToken = default)
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

        _entries.AddRange(request.Entries);
        Version = new SessionVersion(_entries.Count);

        return ValueTask.FromResult<SessionAppendResult>(new SessionAppended(Version, request.Entries));
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ReadOverride is not null)
        {
            return ValueTask.FromResult(ReadOverride(request));
        }

        if (request.BranchId != BranchId)
        {
            return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(request.Context.ToAddress()));
        }

        var start = (int) request.FromSequenceExclusive.Value;
        var page = _entries.Skip(start).Take(request.PageSize).ToImmutableArray();
        var throughSequence = page.IsEmpty ? request.FromSequenceExclusive : new SessionSequence(start + page.Length);
        var hasMore = start + page.Length < _entries.Count;

        return ValueTask.FromResult<SessionPageResult>(new SessionPage(page, throughSequence, hasMore));
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support session creation.");

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support session loading.");

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support branching.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake does not support deletion.");
}
