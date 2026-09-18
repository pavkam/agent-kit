// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;
/// <summary>Verifies DefaultSessionStoreCatalog behavior and contracts.</summary>
public sealed class DefaultSessionStoreCatalogTests
{
    [Fact]
    public void CatalogConstructor_WhenStoreDescriptorMutationIsInvalid_RejectsBeforeCapturingBinding()
    {
        var store = new FakeSessionStore();
        var descriptor = store.Descriptor;
        store.OnDescriptor = () => descriptor with
        {
            Key = default
        };
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultSessionStoreCatalog([store]));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
        store.DescriptorReadCount.ShouldBe(2);
    }

    [Fact]
    public void GetDescriptors_WhenStoresAreProvided_ReturnsAStableKeyOrderedSnapshot()
    {
        var store = new FakeSessionStore();
        var catalog = new DefaultSessionStoreCatalog([store]);
        catalog.GetDescriptors().ShouldBe([store.Descriptor]);
    }

    [Fact]
    public void GetDescriptors_WhenMultipleStoresAreProvided_OrdersByKeyOrdinally()
    {
        var zebra = new FakeSessionStoreWithKey("zebra");
        var alpha = new FakeSessionStoreWithKey("alpha");

        var catalog = new DefaultSessionStoreCatalog([zebra, alpha]);

        catalog.GetDescriptors().Select(static descriptor => descriptor.Key.Value)
            .ShouldBe(["alpha", "zebra"]);
    }

    private sealed class FakeSessionStoreWithKey: ISessionStore
    {
        public FakeSessionStoreWithKey(string key) =>
            Descriptor = new SessionStoreDescriptor(new SessionStoreKey(key), SessionStoreCapabilities.None,
                SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false);

        public ComponentId SecurityAudience { get; } = new("agentkit.session.tests.keyed-store");
        public SessionStoreDescriptor Descriptor { get; }

        public ValueTask<SessionCreateResult> CreateAsync(AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(AuthorizedSessionStoreRequest<SessionOperationContext> context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
            AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(
            AuthorizedSessionStoreRequest<SessionLaneStateRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(AuthorizedSessionStoreRequest<SessionAppendRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(AuthorizedSessionStoreRequest<SessionReadRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> CreateBranchAsync(AuthorizedSessionStoreRequest<SessionBranchRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(AuthorizedSessionStoreRequest<SessionDeleteRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionInputLookupResult> LookupInputAsync(
            AuthorizedSessionStoreRequest<SessionInputLookupRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<InputAdmissionResult> AdmitInputAsync(
            AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStartResult> AcceptRunAsync(
            AuthorizedSessionStoreRequest<SessionRunStartRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStateResult> LoadRunStateAsync(
            AuthorizedSessionStoreRequest<SessionRunStateRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
            AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
