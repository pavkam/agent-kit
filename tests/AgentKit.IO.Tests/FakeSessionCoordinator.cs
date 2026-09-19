// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>A scripted <see cref="ISessionCoordinator"/> exercising only the members <see cref="SessionBackedInputQueue"/> calls.</summary>
internal sealed class FakeSessionCoordinator: ISessionCoordinator
{
    public Func<SessionOperationContext, SessionLoadResult>? OnLoad { get; set; }
    public Func<SessionLaneStateRequest, SessionLaneStateResult>? OnLoadLaneState { get; set; }
    public Func<SessionPendingInputsRequest, SessionPendingInputsResult>? OnLoadPendingInputs { get; set; }
    public Func<SessionInputAdmissionRequest, InputAdmissionResult>? OnAdmitInput { get; set; }
    public Func<SessionInputPromotionRequest, SessionInputPromotionResult>? OnPromoteInput { get; set; }
    public SessionInputAdmissionRequest? ReceivedAdmission { get; private set; }
    public SessionInputPromotionRequest? ReceivedPromotion { get; private set; }

    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnLoad?.Invoke(context) ?? throw new NotSupportedException());

    public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(
        SessionLaneStateRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnLoadLaneState?.Invoke(request) ?? throw new NotSupportedException());

    public ValueTask<SessionPendingInputsResult> LoadPendingInputsAsync(
        SessionPendingInputsRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnLoadPendingInputs?.Invoke(request) ?? throw new NotSupportedException());

    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        SessionInputAdmissionRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ReceivedAdmission = request;
        return ValueTask.FromResult(OnAdmitInput?.Invoke(request) ?? throw new NotSupportedException());
    }

    public ValueTask<SessionInputPromotionResult> PromoteInputAsync(
        SessionInputPromotionRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ReceivedPromotion = request;
        return ValueTask.FromResult(OnPromoteInput?.Invoke(request) ?? throw new NotSupportedException());
    }
}
