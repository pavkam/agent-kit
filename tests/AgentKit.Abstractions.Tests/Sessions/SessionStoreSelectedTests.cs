// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreSelected behavior and contracts.</summary>
public sealed class SessionStoreSelectedTests
{
    [Fact]
    public void Constructor_WhenStoreIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreSelected(null!, Descriptor()));
        exception.ParamName.ShouldBe("store");
    }

    [Fact]
    public void Constructor_WhenDescriptorIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreSelected(new UnsupportedStore(), null!));
        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var store = new UnsupportedStore();
        var descriptor = Descriptor();
        var selected = new SessionStoreSelected(store, descriptor);
        selected.Store.ShouldBeSameAs(store);
        selected.Descriptor.ShouldBe(descriptor);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionStoreSelected(new UnsupportedStore(), Descriptor());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionStoreDescriptor Descriptor() =>
        new(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false);

    private sealed class UnsupportedStore: ISessionStore
    {
        public ComponentId SecurityAudience => new("store");
        public SessionStoreDescriptor Descriptor => new(new SessionStoreKey("store"), SessionStoreCapabilities.None, SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false);
        public ValueTask<SessionCreateResult> CreateAsync(AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(AuthorizedSessionStoreRequest<SessionOperationContext> context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(AuthorizedSessionStoreRequest<SessionLaneStateRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(AuthorizedSessionStoreRequest<SessionAppendRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(AuthorizedSessionStoreRequest<SessionReadRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> CreateBranchAsync(AuthorizedSessionStoreRequest<SessionBranchRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(AuthorizedSessionStoreRequest<SessionDeleteRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionInputLookupResult> LookupInputAsync(AuthorizedSessionStoreRequest<SessionInputLookupRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<InputAdmissionResult> AdmitInputAsync(AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStartResult> AcceptRunAsync(AuthorizedSessionStoreRequest<SessionRunStartRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStateResult> LoadRunStateAsync(AuthorizedSessionStoreRequest<SessionRunStateRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPendingInputsResult> LoadPendingInputsAsync(AuthorizedSessionStoreRequest<SessionPendingInputsRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionInputPromotionResult> PromoteInputAsync(AuthorizedSessionStoreRequest<SessionInputPromotionRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
