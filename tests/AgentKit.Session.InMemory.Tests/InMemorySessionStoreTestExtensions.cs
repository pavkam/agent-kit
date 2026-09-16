// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Adapts legacy behavior tests to the protected store boundary using exact per-call grants.</summary>
internal static class InMemorySessionStoreTestExtensions
{
    extension(InMemorySessionStore store)
    {
        internal ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, CancellationToken cancellationToken = default)
        {
            var security = TestSecurityHarness.For(store);
            var lower = security.Lower(request);
            return store.CreateAsync(security.Authorize(store, lower, SecurityOperationKind.StateMutation, SecurityEffect.Create), cancellationToken);
        }

        internal ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext request, CancellationToken cancellationToken = default) =>
            store.LoadAsync(TestSecurityHarness.For(store).Authorize(store, request, SecurityOperationKind.StateRead, SecurityEffect.Observe), cancellationToken);

        internal ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, CancellationToken cancellationToken = default) =>
            store.AppendAsync(TestSecurityHarness.For(store).Authorize(store, request, SecurityOperationKind.StateMutation, SecurityEffect.Append), cancellationToken);

        internal ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, CancellationToken cancellationToken = default) =>
            store.ReadAsync(TestSecurityHarness.For(store).Authorize(store, request, SecurityOperationKind.StateRead, SecurityEffect.Observe), cancellationToken);

        internal ValueTask<SessionBranchResult> CreateBranchAsync(SessionBranchRequest request, CancellationToken cancellationToken = default) =>
            store.CreateBranchAsync(TestSecurityHarness.For(store).Authorize(store, request, SecurityOperationKind.StateMutation, SecurityEffect.Create), cancellationToken);

        internal ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, CancellationToken cancellationToken = default) =>
            store.DeleteAsync(TestSecurityHarness.For(store).Authorize(store, request, SecurityOperationKind.StateMutation, SecurityEffect.Delete), cancellationToken);

        internal ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
            SessionExecutionLaneProvisionRequest request, CancellationToken cancellationToken = default) =>
            store.ProvisionLaneAsync(TestSecurityHarness.For(store).Authorize(
                store, request, SecurityOperationKind.StateMutation, SecurityEffect.Create), cancellationToken);

        internal ValueTask<InputAdmissionResult> AdmitInputAsync(
            SessionInputAdmissionRequest request, CancellationToken cancellationToken = default) =>
            store.AdmitInputAsync(TestSecurityHarness.For(store).Authorize(
                store, request, SecurityOperationKind.StateMutation, SecurityEffect.Append), cancellationToken);

        internal ValueTask<SessionRunStartResult> AcceptRunAsync(
            SessionRunStartRequest request, CancellationToken cancellationToken = default) =>
            store.AcceptRunAsync(TestSecurityHarness.For(store).Authorize(
                store, request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), cancellationToken);

        internal ValueTask<SessionRunStateResult> LoadRunStateAsync(
            SessionRunStateRequest request, CancellationToken cancellationToken = default) =>
            store.LoadRunStateAsync(TestSecurityHarness.For(store).Authorize(
                store, request, SecurityOperationKind.StateRead, SecurityEffect.Observe), cancellationToken);

        internal ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
            SessionRunReleaseRequest request, CancellationToken cancellationToken = default) =>
            store.ReleaseRunAsync(TestSecurityHarness.For(store).Authorize(
                store, request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), cancellationToken);
    }
}
