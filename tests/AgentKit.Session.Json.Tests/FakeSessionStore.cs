// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>A minimal foreign <see cref="ISessionStore"/> used only to prove additive multi-store registration.</summary>
/// <remarks>No method is ever invoked; the type exists solely to be resolved from dependency injection alongside the JSON store.</remarks>
internal sealed class FakeSessionStore: ISessionStore
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.fake.session.store");

    /// <inheritdoc/>
    public SessionStoreDescriptor Descriptor { get; } = new(
        new SessionStoreKey("test.fake"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong,
        durable: false, supportsDistributedFencing: false);

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        AuthorizedSessionStoreRequest<SessionOperationContext> context, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(
        AuthorizedSessionStoreRequest<SessionLaneStateRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        AuthorizedSessionStoreRequest<SessionAppendRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        AuthorizedSessionStoreRequest<SessionReadRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> CreateBranchAsync(
        AuthorizedSessionStoreRequest<SessionBranchRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        AuthorizedSessionStoreRequest<SessionDeleteRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionInputLookupResult> LookupInputAsync(
        AuthorizedSessionStoreRequest<SessionInputLookupRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionRunStartResult> AcceptRunAsync(
        AuthorizedSessionStoreRequest<SessionRunStartRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(
        AuthorizedSessionStoreRequest<SessionRunStateRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
        AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionPendingInputsResult> LoadPendingInputsAsync(
        AuthorizedSessionStoreRequest<SessionPendingInputsRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionInputPromotionResult> PromoteInputAsync(
        AuthorizedSessionStoreRequest<SessionInputPromotionRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake store is never invoked.");
}
