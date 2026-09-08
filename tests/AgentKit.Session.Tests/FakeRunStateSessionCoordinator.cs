// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>Supplies deterministic protected run-state results to local ownership tests.</summary>
internal sealed class FakeRunStateSessionCoordinator: ISessionCoordinator
{
    /// <summary>Gets or sets the run-state load handler.</summary>
    internal Func<SessionRunStateRequest, SessionProfileSnapshot, CancellationToken,
        ValueTask<SessionRunStateResult>>? OnLoadRunState
    { get; set; }

    /// <summary>Gets the number of run-state loads.</summary>
    internal int LoadCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(SessionRunStateRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return OnLoadRunState?.Invoke(request, session.Profile, cancellationToken)
            ?? ValueTask.FromResult<SessionRunStateResult>(
                new SessionRunStateUnavailable("No accepted state was configured."));
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
