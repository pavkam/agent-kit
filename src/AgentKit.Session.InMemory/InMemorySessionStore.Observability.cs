// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

public sealed partial class InMemorySessionStore
{
    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "create",
            request.AgentId,
            sessionId: null,
            token => CreateCoreAsync(request, token),
            static result => result is SessionCreated,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "load",
            context.AgentId,
            context.SessionId,
            token => LoadCoreAsync(context, token),
            static result => result is SessionLoaded,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "append",
            request.Context.AgentId,
            request.Context.SessionId,
            token => AppendCoreAsync(request, token),
            static result => result is SessionAppended,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "read",
            request.Context.AgentId,
            request.Context.SessionId,
            token => ReadCoreAsync(request, token),
            static result => result is SessionPage,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> CreateBranchAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "branch",
            request.Context.AgentId,
            request.Context.SessionId,
            token => CreateBranchCoreAsync(request, token),
            static result => result is SessionBranched,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionStoreObservability.ObserveAsync(
            _logger,
            "delete",
            request.Context.AgentId,
            request.Context.SessionId,
            token => DeleteCoreAsync(request, token),
            static result => result is SessionDeleted,
            cancellationToken);
    }
}
