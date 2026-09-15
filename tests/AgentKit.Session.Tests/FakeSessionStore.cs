// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>
/// A scripted <see cref="ISessionStore"/> test double that returns
/// pre-configured results and records every call it received, so
/// <see cref="DefaultSessionCoordinator"/> behavior can be tested in
/// isolation from real storage logic.
/// </summary>
internal sealed class FakeSessionStore: ISessionStore
{
    private SessionStoreDescriptor DescriptorValue { get; } = new(
        new SessionStoreKey("fake"), SessionStoreCapabilities.Branching, SessionConsistencyModel.Strong,
        durable: false, supportsDistributedFencing: false);

    public ComponentId SecurityAudience { get; } = new("agentkit.session.tests.fake-store");

    public int DescriptorReadCount { get; private set; }

    public Func<SessionStoreDescriptor>? OnDescriptor { get; set; }

    public SessionStoreDescriptor Descriptor
    {
        get
        {
            DescriptorReadCount++;
            return OnDescriptor?.Invoke() ?? DescriptorValue;
        }
    }

    public Func<SessionStoreCreateRequest, SessionCreateResult>? OnCreate { get; set; }

    public Func<SessionAppendRequest, SessionAppendResult>? OnAppend { get; set; }

    public Func<SessionBranchRequest, SessionBranchResult>? OnBranch { get; set; }

    public Func<SessionDeleteRequest, SessionDeleteResult>? OnDelete { get; set; }

    public Func<SessionOperationContext, SessionLoadResult>? OnLoad { get; set; }

    public Func<SessionReadRequest, SessionPageResult>? OnRead { get; set; }

    public List<SessionAppendRequest> ReceivedAppends { get; } = [];

    public List<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> ReceivedCreates { get; } = [];

    public List<AuthorizedSessionStoreRequest<SessionOperationContext>> ReceivedLoads { get; } = [];

    public ValueTask<SessionCreateResult> CreateAsync(AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request,
        CancellationToken cancellationToken = default)
    {
        ReceivedCreates.Add(request);
        return ValueTask.FromResult(OnCreate?.Invoke(request.Request) ?? new SessionCreateFailed("not configured"));
    }

    public ValueTask<SessionLoadResult> LoadAsync(AuthorizedSessionStoreRequest<SessionOperationContext> context,
        CancellationToken cancellationToken = default)
    {
        ReceivedLoads.Add(context);
        return ValueTask.FromResult(OnLoad?.Invoke(context.Request) ?? new SessionLoadFailed("not configured"));
    }

    public ValueTask<SessionAppendResult> AppendAsync(AuthorizedSessionStoreRequest<SessionAppendRequest> request,
        CancellationToken cancellationToken = default)
    {
        ReceivedAppends.Add(request.Request);
        return ValueTask.FromResult(OnAppend?.Invoke(request.Request) ?? new SessionAppendFailed("not configured"));
    }

    public ValueTask<SessionPageResult> ReadAsync(AuthorizedSessionStoreRequest<SessionReadRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnRead?.Invoke(request.Request) ?? new SessionReadFailed("not configured"));

    public ValueTask<SessionBranchResult> CreateBranchAsync(AuthorizedSessionStoreRequest<SessionBranchRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnBranch?.Invoke(request.Request) ?? new SessionBranchFailed("not configured"));

    public ValueTask<SessionDeleteResult> DeleteAsync(AuthorizedSessionStoreRequest<SessionDeleteRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnDelete?.Invoke(request.Request) ?? new SessionDeleteFailed("not configured"));

    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SessionExecutionLaneProvisionResult>(new SessionExecutionLaneProvisionRejected("not configured"));

    public ValueTask<SessionInputLookupResult> LookupInputAsync(
        AuthorizedSessionStoreRequest<SessionInputLookupRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SessionInputLookupResult>(new SessionInputLookupRejected("not configured"));

    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
            new InputRejection(InputRejectionKind.Unauthorized, "not configured")));

    public ValueTask<SessionRunStartResult> AcceptRunAsync(
        AuthorizedSessionStoreRequest<SessionRunStartRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartRejected("not configured"));

    public ValueTask<SessionRunStateResult> LoadRunStateAsync(
        AuthorizedSessionStoreRequest<SessionRunStateRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateUnavailable("not configured"));

    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
        AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SessionRunReleaseResult>(
            new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Unsupported, "not configured"));
}
