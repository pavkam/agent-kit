// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>A deterministic, configurable <see cref="ISessionCoordinator"/> fake used to drive <see cref="DefaultConversationSession"/> tests.</summary>
/// <remarks>Only the members <see cref="DefaultConversationSession"/> actually calls are meaningfully implemented; the rest throw <see cref="NotSupportedException"/>.</remarks>
internal sealed class FakeSessionCoordinator: ISessionCoordinator
{
    /// <summary>Gets the fixed session identity this fake assigns to every created session.</summary>
    public SessionId SessionId { get; } = new(Guid.NewGuid());

    /// <summary>Gets the fixed branch identity this fake assigns to every created session.</summary>
    public BranchId BranchId { get; } = new(Guid.NewGuid());

    /// <summary>Gets the number of times <see cref="CreateAsync"/> was called.</summary>
    public int CreateCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="LoadAsync"/> was called.</summary>
    public int LoadCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="AppendAsync"/> was called.</summary>
    public int AppendCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="ReadAsync"/> was called.</summary>
    public int ReadCallCount { get; private set; }

    /// <summary>Gets or sets the result <see cref="CreateAsync"/> returns; a successful descriptor by default.</summary>
    public SessionCreateResult? CreateResult { get; set; }

    /// <summary>Gets or sets the result <see cref="LoadAsync"/> returns; a successful load by default.</summary>
    public SessionLoadResult? LoadResult { get; set; }

    /// <summary>Gets or sets the result <see cref="AppendAsync"/> returns; a successful append by default.</summary>
    public SessionAppendResult? AppendResult { get; set; }

    /// <summary>Gets or sets the bounded discovery result.</summary>
    public SessionDirectoryListResult? ListResult { get; set; }

    /// <summary>Gets or sets a request-aware history result factory.</summary>
    public Func<SessionReadRequest, SessionPageResult>? ReadResultFactory { get; set; }

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryListResult> ListAsync(
        SessionDirectoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ListResult ??
            new SessionDirectoryPage([], null));
    }

    /// <summary>Gets the most recent append request observed, for asserting session/branch reuse.</summary>
    public SessionAppendRequest? LastAppendRequest { get; private set; }

    /// <summary>Gets the most recent history read request.</summary>
    public SessionReadRequest? LastReadRequest { get; private set; }

    /// <summary>Gets the exact profile supplied with the most recent history read.</summary>
    public SessionProfileSnapshot? LastReadProfile { get; private set; }

    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        CreateCallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        if (CreateResult is not null)
        {
            return ValueTask.FromResult(CreateResult);
        }

        var now = DateTimeOffset.UnixEpoch;
        var descriptor = new SessionDescriptor(
            new SessionAddress(request.AgentId, SessionId),
            request.ConversationId,
            request.Identity.TenantId,
            request.Identity.PrincipalId,
            new SessionStoreKey("test-store"),
            BranchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            now,
            now,
            new SchemaVersion("1"),
            ExtensionData.Empty);
        return ValueTask.FromResult<SessionCreateResult>(new SessionCreated(descriptor, existing: false));
    }

    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        if (LoadResult is not null)
        {
            return ValueTask.FromResult(LoadResult);
        }

        var now = DateTimeOffset.UnixEpoch;
        var descriptor = new SessionDescriptor(
            new SessionAddress(context.AgentId, context.SessionId),
            null,
            context.Identity.TenantId,
            context.Identity.PrincipalId,
            new SessionStoreKey("test-store"),
            BranchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            now,
            now,
            new SchemaVersion("1"),
            ExtensionData.Empty);
        return ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(descriptor));
    }

    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        AppendCallCount++;
        LastAppendRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return AppendResult is not null
            ? ValueTask.FromResult(AppendResult)
            : ValueTask.FromResult<SessionAppendResult>(
                new SessionAppended(new SessionVersion(request.ExpectedVersion.Value + 1), request.Entries));
    }

    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ReadCallCount++;
        LastReadRequest = request;
        LastReadProfile = profile;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            ReadResultFactory?.Invoke(request)
            ?? new SessionPage(
                [],
                request.FromSequenceExclusive,
                hasMore: false,
                request.Snapshot ?? new SessionReadSnapshot(
                    request.Context.ToAddress(),
                    request.BranchId,
                    new SessionVersion(0),
                    new SessionSequence(0))));
    }

    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("DefaultConversationSession never calls BranchAsync.");

    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("DefaultConversationSession never calls DeleteAsync.");
}
