// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public async Task AddInMemoryArtifactStore_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var fixture = new StoreFixture();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
        var grants = new IntentReceiptGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(fixture.Clock);
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddInMemoryArtifactStore();
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IArtifactStore>().PrepareAsync(fixture.CreatePrepare("content"u8.ToArray()), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<ArtifactPrepared>();
        grants.LastIntent.ShouldNotBeNull().Id.ShouldBe(expectedId);
    }

    private sealed class StoreFixture
    {
        private int _sequence;
        internal StoreFixture()
        {
            Clock = new FakeTimeProvider(_now);
            Grants = new InMemorySecurityGrantStore(Clock);
            Store = new InMemoryArtifactStore(Grants, Clock);
        }

        internal FakeTimeProvider Clock { get; }
        internal InMemorySecurityGrantStore Grants { get; }
        internal InMemoryArtifactStore Store { get; }
        internal ExecutionIdentity Identity { get; } = CreateIdentity("tenant", "principal");

        internal async Task<ArtifactReference> CommitAsync(byte[] bytes, bool legalHold = false)
        {
            var prepare = CreatePrepare(bytes, legalHold: legalHold);
            await RegisterPrepareGrantAsync(prepare);
            _ = await Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
            var finalize = CreateFinalize(prepare.PreparationId, prepare.Identity);
            await RegisterFinalizeGrantAsync(finalize);
            return (await Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactFinalized>().Reference;
        }

        internal ArtifactStorePrepareRequest CreatePrepare(byte[] bytes, string idempotencyKey = "prepare", DateTimeOffset? expiresAt = null, InputFingerprint? grantFingerprint = null, bool legalHold = false, ArtifactId? artifactId = null, ArtifactVersion? version = null, ArtifactPreparationId? preparationId = null, ExecutionIdentity? identity = null, TenantId? tenantId = null, PrincipalId? createdBy = null, DateTimeOffset? createdAt = null, bool capturedAuthorization = false)
        {
            var selectedArtifactId = artifactId ?? new ArtifactId(NextGuid());
            var selectedPreparationId = preparationId ?? new ArtifactPreparationId(NextGuid());
            var selectedIdentity = identity ?? Identity;
            var selectedTenantId = tenantId ?? selectedIdentity.TenantId;
            var selectedCreatedBy = createdBy ?? selectedIdentity.PrincipalId;
            var selectedCreatedAt = createdAt ?? _now;
            var selectedExpiresAt = expiresAt ?? selectedCreatedAt.AddMinutes(5);
            var scope = CreateScope();
            var metadata = new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", bytes.LongLength, FileSecurityBinding.ContentFingerprint(bytes), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, legalHold));
            var grant = CreateGrant(scope, selectedIdentity, SecurityEffect.Create, [ArtifactSecurityBinding.ArtifactResource(selectedArtifactId), ArtifactSecurityBinding.PreparationResource(selectedPreparationId)], grantFingerprint ?? ArtifactSecurityBinding.PrepareFingerprint(selectedArtifactId, selectedPreparationId, version ?? new ArtifactVersion("1"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), selectedTenantId, selectedCreatedBy, new ArtifactDirectoryId("tool-output"), metadata, selectedCreatedAt, selectedExpiresAt), capturedAuthorization);
            return new ArtifactStorePrepareRequest(selectedArtifactId, selectedPreparationId, version ?? new ArtifactVersion("1"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), selectedTenantId, selectedCreatedBy, new ArtifactDirectoryId("tool-output"), metadata, [.. bytes], selectedCreatedAt, selectedExpiresAt, scope, selectedIdentity, grant, new IdempotencyKey(idempotencyKey));
        }

        internal ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.CreateOrReplace, [ArtifactSecurityBinding.PreparationResource(preparationId)], ArtifactSecurityBinding.FinalizeFingerprint(preparationId));
            return new ArtifactStoreFinalizeRequest(preparationId, scope, identity, grant, new IdempotencyKey($"finalize-{NextGuid()}"));
        }

        internal ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Delete, [ArtifactSecurityBinding.PreparationResource(preparationId)], ArtifactSecurityBinding.AbortFingerprint(preparationId, ArtifactAbortReason.Cancelled));
            return new ArtifactStoreAbortRequest(preparationId, ArtifactAbortReason.Cancelled, scope, identity, grant, new IdempotencyKey($"abort-{NextGuid()}"));
        }

        internal ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(reference.Id)], ArtifactSecurityBinding.ReadFingerprint(reference));
            return new ArtifactStoreReadRequest(reference, scope, identity, grant);
        }

        internal ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Delete, [ArtifactSecurityBinding.ArtifactResource(reference.Id)], ArtifactSecurityBinding.DeleteFingerprint(reference));
            return new ArtifactStoreDeleteRequest(reference, scope, identity, grant, new IdempotencyKey($"delete-{NextGuid()}"));
        }

        internal SecurityGrant CreateGrant(SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityEffect effect, ImmutableArray<ProtectedResource> resources, InputFingerprint fingerprint, bool capturedAuthorization = false)
        {
            var id = new GrantId(NextGuid());
            var requestId = new SecurityRequestId(NextGuid());
            var policyVersion = new SecurityPolicyVersion(1);
            var revocationVersion = new SecurityRevocationVersion(1);
            return capturedAuthorization ? new SecurityGrant(id, requestId, scope, identity, new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("11000000-0000-0000-0000-000000000011")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, identity), Store.SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint, policyVersion, revocationVersion, _now, _now.AddHours(1), 1) : new SecurityGrant(id, requestId, scope, identity, Store.SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint, policyVersion, revocationVersion, _now, _now.AddHours(1), 1);
        }

        internal ValueTask RegisterPrepareGrantAsync(ArtifactStorePrepareRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterFinalizeGrantAsync(ArtifactStoreFinalizeRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterAbortGrantAsync(ArtifactStoreAbortRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterReadGrantAsync(ArtifactStoreReadRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterDeleteGrantAsync(ArtifactStoreDeleteRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal SecurityAuthorizationScope CreateScope() => new(new AgentId(NextGuid()), new SessionId(NextGuid()), new InRunOperationCorrelation(new OperationId(NextGuid()), new RunId(NextGuid()), null));
        internal static ExecutionIdentity CreateIdentity(string tenant, string principal) => TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);
        private Guid NextGuid()
        {
            _sequence++;
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, _sequence);
            return new Guid(bytes);
        }
    }

    private sealed class IntentReceiptGrantStore: ISecurityGrantStore
    {
        internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
        internal bool IncludeReceipt { get; set; } = true;
        internal bool ReturnExactReceipt { get; set; } = true;
        internal Action? OnConsumption { get; set; }
        internal SecurityEnforcementIntent? LastIntent { get; private set; }
        internal int ConsumptionCount { get; private set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
        {
            _ = grant;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) => ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Legacy consumption is unsupported."));
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsumptionCount++;
            LastIntent = intent;
            OnConsumption?.Invoke();
            var receipt = IncludeReceipt && (Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled) ? new SecurityEnforcementIntentReceipt(ReturnExactReceipt ? intent.Id : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")), grant.Id, grant.RequestId, enforcement, intent.RequiredFence, SecurityEnforcementBinding.Fingerprint(enforcement, intent), _now) : null;
            return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied.", receipt));
        }

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
        }
    }

    private sealed class SequenceSecurityEnforcementIntentIdGenerator(params Guid[] values): IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        private readonly Queue<Guid> _values = new(values);
        public SecurityEnforcementIntentId Create() => new(_values.Dequeue());
    }

}
