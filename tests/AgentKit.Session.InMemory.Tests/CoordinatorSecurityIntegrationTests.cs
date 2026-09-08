// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Permissions;
using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

public sealed class CoordinatorSecurityIntegrationTests
{
    [Fact]
    public async Task CreateAsync_WithRealGrantStoreDirectoryAndStore_ConsumesIndependentExactGrants()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("test-directory"));
        _ = services.RemoveAll<ISecurityAuditDispatcher>();
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(new IntegrationProfileSelector());
        _ = services.RemoveAll<ISecurityAuthoritySelector>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(provider =>
            new IntegrationAuthoritySelector(new RegisteringAuthority(
                provider.GetRequiredService<ISecurityGrantStore>(), timeProvider)));
        await using var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<ISessionCoordinator>();
        var request = TestFactory.CreateRequest(idempotencyKey: new IdempotencyKey("integrated-create"));

        var result = await coordinator.CreateAsync(
            request,
            TestSecurityEvidence.SessionProfile("agentkit.in-memory"),
            TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<SessionCreated>();
        created.Descriptor.Address.AgentId.ShouldBe(request.AgentId);
        created.Descriptor.StoreKey.ShouldBe(new SessionStoreKey("agentkit.in-memory"));
    }

    private sealed class IntegrationProfileSelector: ISecurityProfileSelector
    {
        public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
            SecurityAuthorizationCaptureRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
                new SecurityAuthorizationContext(
                    request.ProfileKey,
                    new SecurityProfileVersion(1),
                    new SecurityPolicySnapshotReference(
                        new SecurityPolicySnapshotId(
                            Guid.Parse("22222222-2222-2222-2222-222222222222")),
                        new SecurityPolicyVersion(1),
                        new ContentHash("sha256:policy")),
                    new ComponentKey<ISecurityAuthority>("authority"),
                    request.AgentDefinitionRevision,
                    request.ConfigurationVersion,
                    request.Scope,
                    request.Identity)));
        }
    }

    private sealed class IntegrationAuthoritySelector(ISecurityAuthority authority): ISecurityAuthoritySelector
    {
        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
            SecurityAuthorizationContext authorization,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
                new SecurityAuthoritySelected(authorization, authority));
        }
    }

    private sealed class RegisteringAuthority(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider): ISecurityAuthority
    {
        public async ValueTask<SecurityDecision> AuthorizeAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            var authorization = request.Authorization
                ?? throw new InvalidOperationException("Coordinator requests must retain captured authorization.");
            var grant = new SecurityGrant(
                new GrantId(Guid.NewGuid()),
                request.Id,
                request.Scope,
                request.Identity,
                authorization,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                authorization.PolicySnapshot.Version,
                new SecurityRevocationVersion(1),
                timeProvider.GetUtcNow(),
                request.Deadline,
                allowedUses: 1);
            await grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
            return new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant);
        }
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
        }
    }
}
