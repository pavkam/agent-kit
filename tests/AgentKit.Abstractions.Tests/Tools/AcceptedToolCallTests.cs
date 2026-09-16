// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies AcceptedToolCall behavior and contracts.</summary>
public sealed class AcceptedToolCallTests
{
    [Fact]
    public void AcceptedToolCall_Constructor_WhenValid_RetainsCompleteEvidence()
    {
        var fixture = Fixture.Create();
        var accepted = fixture.Accepted();
        accepted.AgentId.ShouldBe(fixture.AgentId);
        accepted.SessionId.ShouldBe(fixture.SessionId);
        accepted.TurnId.ShouldBe(fixture.TurnId);
        accepted.OperationId.ShouldBe(fixture.OperationId);
        accepted.Authorization.ShouldBe(fixture.Authorization);
        accepted.Acceptance.ShouldBe(fixture.Acceptance);
        accepted.ProviderAlias.ShouldBe(new ToolAlias("provider-tool"));
        accepted.ToolId.ShouldBe(fixture.ToolId);
        accepted.ToolVersion.ShouldBe(fixture.ToolVersion);
        accepted.ExternalIdempotencyKey.ShouldBeNull();
        accepted.Admission.ShouldBe(fixture.Admission);
        accepted.RequestedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        _ = accepted.Normalization.ExecutionPolicy.ShouldNotBeNull();
        accepted.ProjectionPolicy.ShouldBe(fixture.ProjectionPolicy);
    }

    [Fact]
    public void AcceptedToolCall_Constructor_WhenKeyedCallHasExternalKey_RetainsKey()
    {
        var fixture = Fixture.Create(idempotency: IdempotencyClassification.IdempotentWithKey);
        var accepted = fixture.Accepted();
        accepted.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var fixture = Fixture.Create();
        var original = fixture.Accepted();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void AcceptedToolCall_Constructor_WhenAuthorizationDoesNotMatch_ThrowsExactException()
    {
        var fixture = Fixture.Create();
        var wrongAuthorization = TestSupport.TestSecurityEvidence.Authorization(new AgentId(Guid.NewGuid()), fixture.SessionId, fixture.Correlation, fixture.Identity);
        var exception = Should.Throw<ArgumentException>(() => fixture.Accepted(authorization: wrongAuthorization));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void AcceptedToolCall_Constructor_WhenKeyedCallHasNoExternalKey_ThrowsExactException()
    {
        var fixture = Fixture.Create(idempotency: IdempotencyClassification.IdempotentWithKey);
        var exception = Should.Throw<ArgumentException>(() => fixture.Accepted(omitExternalKey: true));
        exception.ParamName.ShouldBe("externalIdempotencyKey");
    }

    private sealed class Fixture
    {
        private Fixture(ToolEffect effect, IdempotencyClassification? idempotency, bool includeExecutionPolicy)
        {
            AgentId = new AgentId(Guid.NewGuid());
            SessionId = new SessionId(Guid.NewGuid());
            RunId = new RunId(Guid.NewGuid());
            TurnId = new TurnId(Guid.NewGuid());
            OperationId = new OperationId(Guid.NewGuid());
            CallId = new ToolCallId(Guid.NewGuid());
            Correlation = new InRunOperationCorrelation(OperationId, RunId, TurnId);
            Identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
            Authorization = TestSupport.TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);
            Acceptance = new ToolCallAcceptanceEvidence(new GrantId(Guid.NewGuid()), new InputFingerprint("sha256:accepted"), DateTimeOffset.UnixEpoch);
            ToolId = new ToolId("tool");
            ToolVersion = new ToolVersion("1.0");
            Effects = new ToolEffects(effect, idempotency, null);
            Admission = new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog"), 0, new InputFingerprint("sha256:raw"));
            ProjectionPolicy = new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));
            var executionPolicy = includeExecutionPolicy ? new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("execution"), new ToolExecutionPolicyVersion(1)) : null;
            Normalization = new ToolResultNormalizationSnapshot(new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1)), ProjectionPolicy, executionPolicy, new ToolResultNormalizationAlgorithmVersion(1), new ToolResultBounds(1024, 4), ToolResultProjectionTransformations.None, ExtensionData.Empty);
            ExternalKey = idempotency is IdempotencyClassification.IdempotentWithKey ? new IdempotencyKey("external") : null;
        }

        public AgentId AgentId { get; }
        public SessionId SessionId { get; }
        public RunId RunId { get; }
        public TurnId TurnId { get; }
        public OperationId OperationId { get; }
        public ToolCallId CallId { get; }
        public InRunOperationCorrelation Correlation { get; }
        public ExecutionIdentity Identity { get; }
        public SecurityAuthorizationContext Authorization { get; }
        public ToolCallAcceptanceEvidence Acceptance { get; }
        public ToolId ToolId { get; }
        public ToolVersion ToolVersion { get; }
        public ToolEffects Effects { get; }
        public IdempotencyKey? ExternalKey { get; }
        public ToolCallAdmissionEvidence Admission { get; }
        public ToolResultProjectionPolicyReference ProjectionPolicy { get; }
        public ToolResultNormalizationSnapshot Normalization { get; }

        public static Fixture Create(ToolEffect effect = ToolEffect.ReadOnly, IdempotencyClassification? idempotency = IdempotencyClassification.ReadOnly, bool includeExecutionPolicy = true) => new(effect, idempotency, includeExecutionPolicy);
        public AcceptedToolCall Accepted(SecurityAuthorizationContext? authorization = null, IdempotencyKey? externalKey = default, bool omitExternalKey = false) => new(AgentId, SessionId, RunId, TurnId, OperationId, CallId, authorization ?? Authorization, Acceptance, new ToolAlias("provider-tool"), ToolId, ToolVersion, Effects, omitExternalKey ? null : externalKey ?? ExternalKey, Admission, Normalization, ProjectionPolicy, DateTimeOffset.UnixEpoch);
        public ToolCallResult Result(ToolTerminalStatus status = ToolTerminalStatus.InvocationFailed, bool resolved = true, bool accepted = true, bool retryable = false, bool defaultToolId = false, bool defaultToolVersion = false, bool defaultGrant = false, bool omitExternalKey = false, SideEffectCertainty sideEffectCertainty = SideEffectCertainty.DefinitelyNotPerformed, ImmutableArray<ToolResultContent>? content = null) => new(AgentId, SessionId, RunId, TurnId, OperationId, CallId, Authorization, defaultGrant ? default(GrantId) : accepted ? Acceptance.InvocationGrantId : null, accepted ? Acceptance : null, new ToolAlias("provider-tool"), resolved ? defaultToolId ? default(ToolId) : ToolId : null, resolved ? defaultToolVersion ? default(ToolVersion) : ToolVersion : null, resolved ? Effects : null, resolved && !omitExternalKey ? ExternalKey : null, Admission, status, content ?? [], error: null, sideEffectCertainty, usage: null, retryable, Normalization, new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty), ProjectionPolicy, DateTimeOffset.UnixEpoch, accepted ? DateTimeOffset.UnixEpoch : null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
    }
}
