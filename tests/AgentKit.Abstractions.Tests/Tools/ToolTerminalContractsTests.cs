// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolTerminalContractsTests
{
    [Fact]
    public void AcceptedToolCall_Constructor_WhenValid_RetainsCompleteEvidence()
    {
        var fixture = Fixture.Create();

        var accepted = fixture.Accepted();

        accepted.AgentId.ShouldBe(fixture.AgentId);
        accepted.Authorization.ShouldBe(fixture.Authorization);
        accepted.Acceptance.ShouldBe(fixture.Acceptance);
        accepted.ToolId.ShouldBe(fixture.ToolId);
        _ = accepted.Normalization.ExecutionPolicy.ShouldNotBeNull();
        accepted.ProjectionPolicy.ShouldBe(fixture.ProjectionPolicy);
    }

    [Fact]
    public void AcceptedToolCall_Constructor_WhenAuthorizationDoesNotMatch_ThrowsExactException()
    {
        var fixture = Fixture.Create();
        var wrongAuthorization = TestSupport.TestSecurityEvidence.Authorization(
            new AgentId(Guid.NewGuid()), fixture.SessionId, fixture.Correlation, fixture.Identity);

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

    [Fact]
    public void ValidateAuthorization_WhenAuthorizationNull_ThrowsBeforeDereference()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentNullException>(() => AcceptedToolCall.ValidateAuthorization(
            fixture.AgentId, fixture.SessionId, fixture.RunId, fixture.TurnId, fixture.OperationId, null!));

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void ValidateIdempotency_WhenEffectsNull_ThrowsBeforeDereference()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => AcceptedToolCall.ValidateIdempotency(null!, null));

        exception.ParamName.ShouldBe("effects");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenResolvedPolicyIsMissing_ThrowsExactException()
    {
        var fixture = Fixture.Create(includeExecutionPolicy: false);

        var exception = Should.Throw<ArgumentException>(() => fixture.Result());

        exception.ParamName.ShouldBe("normalization");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenUnresolvedPolicyIsPresent_ThrowsExactException()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentException>(() => fixture.Result(resolved: false));

        exception.ParamName.ShouldBe("normalization");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenPresentToolIdIsDefault_ThrowsExactException()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => fixture.Result(defaultToolId: true));

        exception.ParamName.ShouldBe("toolId");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenPresentToolVersionIsDefault_ThrowsExactException()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => fixture.Result(defaultToolVersion: true));

        exception.ParamName.ShouldBe("toolVersion");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenPresentGrantIsDefault_ThrowsExactException()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => fixture.Result(defaultGrant: true));

        exception.ParamName.ShouldBe("grantId");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenAcceptanceIsUnresolved_ThrowsExactException()
    {
        var fixture = Fixture.Create(includeExecutionPolicy: false);

        var exception = Should.Throw<ArgumentException>(() => fixture.Result(resolved: false, accepted: true));

        exception.ParamName.ShouldBe("toolId");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenUnknownStatus_RetainsExactNumericValue()
    {
        var fixture = Fixture.Create();

        var result = fixture.Result(status: (ToolTerminalStatus) 12345);

        ((int) result.Status).ShouldBe(12345);
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenSuccessLacksAcceptance_ThrowsExactException()
    {
        var fixture = Fixture.Create();

        var exception = Should.Throw<ArgumentNullException>(
            () => fixture.Result(status: ToolTerminalStatus.Succeeded, accepted: false));

        exception.ParamName.ShouldBe("acceptance");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenMutatingStartedCallIsUnsafeToRetry_ThrowsExactException()
    {
        var fixture = Fixture.Create(effect: ToolEffect.Mutating, idempotency: null);

        var exception = Should.Throw<ArgumentException>(() => fixture.Result(retryable: true));

        exception.ParamName.ShouldBe("retryable");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenKeyedResolvedRejectionPrecedesAcceptance_AllowsMissingKey()
    {
        var fixture = Fixture.Create(effect: ToolEffect.Mutating, idempotency: IdempotencyClassification.IdempotentWithKey);

        var result = fixture.Result(accepted: false, omitExternalKey: true);

        result.ToolId.ShouldBe(fixture.ToolId);
        result.ExternalIdempotencyKey.ShouldBeNull();
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenMutatingEffectMayHaveStartedWithoutTimestamp_RejectsUnsafeRetry()
    {
        var fixture = Fixture.Create(effect: ToolEffect.Mutating, idempotency: null);

        var exception = Should.Throw<ArgumentException>(() => fixture.Result(
            accepted: false,
            retryable: true,
            sideEffectCertainty: SideEffectCertainty.Unknown));

        exception.ParamName.ShouldBe("retryable");
    }

    [Fact]
    public void ToolCallResult_Constructor_WhenMutatingEffectDefinitelyDidNotStart_AllowsOrdinaryRetry()
    {
        var fixture = Fixture.Create(effect: ToolEffect.Mutating, idempotency: null);

        var result = fixture.Result(accepted: false, retryable: true);

        result.Retryable.ShouldBeTrue();
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    [Fact]
    public void ToolCallResult_Equality_WhenOrderedContentDiffers_IsStructural()
    {
        var fixture = Fixture.Create();
        var first = fixture.Result(content:
        [
            new ToolResultTextContent("a", TextSemantics.Plain, ExtensionData.Empty),
            new ToolResultTextContent("b", TextSemantics.Plain, ExtensionData.Empty),
        ]);
        var same = fixture.Result(content:
        [
            new ToolResultTextContent("a", TextSemantics.Plain, ExtensionData.Empty),
            new ToolResultTextContent("b", TextSemantics.Plain, ExtensionData.Empty),
        ]);
        var reordered = fixture.Result(content:
        [
            new ToolResultTextContent("b", TextSemantics.Plain, ExtensionData.Empty),
            new ToolResultTextContent("a", TextSemantics.Plain, ExtensionData.Empty),
        ]);

        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(reordered);
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
            Identity = TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
            Authorization = TestSupport.TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);
            Acceptance = new ToolCallAcceptanceEvidence(
                new GrantId(Guid.NewGuid()), new InputFingerprint("sha256:accepted"), DateTimeOffset.UnixEpoch);
            ToolId = new ToolId("tool");
            ToolVersion = new ToolVersion("1.0");
            Effects = new ToolEffects(effect, idempotency, null);
            Admission = new ToolCallAdmissionEvidence(
                new ToolCatalogVersion("catalog"), 0, new InputFingerprint("sha256:raw"));
            ProjectionPolicy = new ToolResultProjectionPolicyReference(
                new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));
            var executionPolicy = includeExecutionPolicy
                ? new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("execution"), new ToolExecutionPolicyVersion(1))
                : null;
            Normalization = new ToolResultNormalizationSnapshot(
                new ToolResultRejectionPolicyReference(
                    new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1)),
                ProjectionPolicy,
                executionPolicy,
                new ToolResultNormalizationAlgorithmVersion(1),
                new ToolResultBounds(1024, 4),
                ToolResultProjectionTransformations.None,
                ExtensionData.Empty);
            ExternalKey = idempotency is IdempotencyClassification.IdempotentWithKey
                ? new IdempotencyKey("external")
                : null;
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

        public static Fixture Create(
            ToolEffect effect = ToolEffect.ReadOnly,
            IdempotencyClassification? idempotency = IdempotencyClassification.ReadOnly,
            bool includeExecutionPolicy = true) => new(effect, idempotency, includeExecutionPolicy);

        public AcceptedToolCall Accepted(
            SecurityAuthorizationContext? authorization = null,
            IdempotencyKey? externalKey = default,
            bool omitExternalKey = false) => new(
            AgentId, SessionId, RunId, TurnId, OperationId, CallId, authorization ?? Authorization, Acceptance,
            new ToolAlias("provider-tool"), ToolId, ToolVersion, Effects,
            omitExternalKey ? null : externalKey ?? ExternalKey, Admission,
            Normalization, ProjectionPolicy, DateTimeOffset.UnixEpoch);

        public ToolCallResult Result(
            ToolTerminalStatus status = ToolTerminalStatus.InvocationFailed,
            bool resolved = true,
            bool accepted = true,
            bool retryable = false,
            bool defaultToolId = false,
            bool defaultToolVersion = false,
            bool defaultGrant = false,
            bool omitExternalKey = false,
            SideEffectCertainty sideEffectCertainty = SideEffectCertainty.DefinitelyNotPerformed,
            ImmutableArray<ToolResultContent>? content = null) => new(
            AgentId, SessionId, RunId, TurnId, OperationId, CallId, Authorization,
            defaultGrant ? default(GrantId) : accepted ? Acceptance.InvocationGrantId : null,
            accepted ? Acceptance : null,
            new ToolAlias("provider-tool"), resolved ? defaultToolId ? default(ToolId) : ToolId : null,
            resolved ? defaultToolVersion ? default(ToolVersion) : ToolVersion : null,
            resolved ? Effects : null, resolved && !omitExternalKey ? ExternalKey : null, Admission, status,
            content ?? [], error: null, sideEffectCertainty, usage: null, retryable, Normalization,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty), ProjectionPolicy,
            DateTimeOffset.UnixEpoch, accepted ? DateTimeOffset.UnixEpoch : null, DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
    }
}
