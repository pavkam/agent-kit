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

    /// <summary>Gets or sets the session-descriptor load handler used to observe an explicit durable lane release.</summary>
    internal Func<SessionOperationContext, SessionProfileSnapshot, CancellationToken,
        ValueTask<SessionLoadResult>>? OnLoad
    { get; set; }

    /// <summary>Gets or sets the durable lane-release handler.</summary>
    internal Func<SessionRunReleaseRequest, SessionProfileSnapshot, CancellationToken,
        ValueTask<SessionRunReleaseResult>>? OnReleaseRun
    { get; set; }

    /// <summary>Gets the number of run-state loads.</summary>
    internal int LoadCount { get; private set; }

    /// <summary>Gets every durable lane-release request this coordinator observed.</summary>
    internal List<SessionRunReleaseRequest> ReleaseCalls { get; } = [];

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
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(SessionRunReleaseRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ReleaseCalls.Add(request);
        return OnReleaseRun?.Invoke(request, session.Profile, cancellationToken)
            ?? ValueTask.FromResult<SessionRunReleaseResult>(new SessionRunReleased(request.ExpectedVersion, existing: false));
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default) =>
        OnLoad?.Invoke(context, profile, cancellationToken)
            ?? throw new NotSupportedException();
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
