// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;
using AgentKit.TestSupport;

using Shouldly;

/// <summary>Verifies ArgumentExceptionExtensions behavior and contracts.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfDefault_WhenArrayIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<int> array = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefault(array));
        exception.ParamName.ShouldBe("array");
    }

    [Fact]
    public void ThrowIfDefault_WhenArrayIsEmpty_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfDefault(ImmutableArray<int>.Empty));
    [Fact]
    public void ThrowIfDefault_WhenArrayIsPopulated_DoesNotThrow()
    {
        ImmutableArray<int> array = [1, 2, 3];
        Should.NotThrow(() => ArgumentException.ThrowIfDefault(array));
    }

    [Fact]
    public void ThrowIfDefault_WhenParamNameSuppliedExplicitly_UsesSuppliedName()
    {
        ImmutableArray<int> array = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefault(array, "customParam"));
        exception.ParamName.ShouldBe("customParam");
    }

    [Fact]
    public void ThrowIfDefault_WhenUsedByExtensionValue_CoversProductionCallSite()
    {
        // AgentKit.Extensibility.ExtensionValue is a production call site for
        // this guard; this proves it is actually wired up end to end rather
        // than only exercised directly.
        var exception = Should.Throw<ArgumentException>(() => new ExtensionValue(default));
        exception.ParamName.ShouldBe("canonicalJson");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsDefault_ThrowsWithInferredParamName()
    {
        ImmutableArray<int> values = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultOrEmpty(values));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultOrEmpty(ImmutableArray<int>.Empty, "items"));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsPopulated_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfDefaultOrEmpty(ImmutableArray.Create(1)));
    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenUsedBySecurityRequest_CoversProductionCallSite()
    {
        var grant = SecurityTestData.Grant();
        var exception = Should.Throw<ArgumentException>(() => new SecurityRequest(grant.RequestId, grant.Scope, null, grant.Identity, grant.Audience, grant.Kind, grant.Effect, [], grant.InputFingerprint, grant.ExpiresAt));
        exception.ParamName.ShouldBe("resources");
    }

    [Fact]
    public void ThrowIfDefaultEmptyOrDuplicate_WhenArrayIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<TurnId> values = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultEmptyOrDuplicate(values));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfDefaultEmptyOrDuplicate_WhenArrayContainsDefaultValue_ThrowsArgumentException()
    {
        ImmutableArray<TurnId> values = [new TurnId(Guid.NewGuid()), default];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultEmptyOrDuplicate(values));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfDefaultEmptyOrDuplicate_WhenArrayContainsDuplicateValue_ThrowsArgumentException()
    {
        var turnId = new TurnId(Guid.NewGuid());
        ImmutableArray<TurnId> values = [turnId, turnId];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultEmptyOrDuplicate(values));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfDefaultEmptyOrDuplicate_WhenArrayIsInitializedAndUnique_DoesNotThrow()
    {
        ImmutableArray<TurnId> values = [new TurnId(Guid.NewGuid()), new TurnId(Guid.NewGuid())];
        Should.NotThrow(() => ArgumentException.ThrowIfDefaultEmptyOrDuplicate(values));
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsAbsolute_DoesNotThrow()
    {
        var uri = new Uri("https://api.example.test/v1/");
        Should.NotThrow(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsRelative_ThrowsArgumentExceptionWithInferredParamName()
    {
        var uri = new Uri("v1/chat/completions", UriKind.Relative);
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));
        exception.ParamName.ShouldBe("uri");
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsNull_ThrowsArgumentNullException()
    {
        Uri uri = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));
        exception.ParamName.ShouldBe("uri");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("/rooted/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("\\evil.example.test\\chat")]
    public void ThrowIfNotRelativeUriPath_WhenPathCanEscapeBaseAddress_ThrowsArgumentException(string path)
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotRelativeUriPath(path));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void ThrowIfNotRelativeUriPath_WhenPathIsNull_ThrowsArgumentNullException()
    {
        string path = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotRelativeUriPath(path));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void ThrowIfNotRelativeUriPath_WhenPathIsRelative_DoesNotThrow()
    {
        const string path = "v1/chat/completions";
        Should.NotThrow(() => ArgumentException.ThrowIfNotRelativeUriPath(path));
    }

    [Fact]
    public void ThrowIfContainsNull_WhenArrayContainsNull_ThrowsWithInferredParameter()
    {
        ImmutableArray<string> values = ["one", null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfContainsNull(values));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfContainsNull_WhenArrayContainsOnlyValues_DoesNotThrow()
    {
        ImmutableArray<string> values = ["one", "two"];
        Should.NotThrow(() => ArgumentException.ThrowIfContainsNull(values));
    }

    [Fact]
    public void ThrowIfContainsNul_WhenValueContainsNul_ThrowsWithInferredParameter()
    {
        const string value = "before\0after";
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfContainsNul(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ThrowIfContainsNul_WhenValueContainsNoNul_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfContainsNul("safe"));
    [Fact]
    public void ThrowIfContainsNul_WhenUsedByProcessEnvironment_CoversProductionCallSite()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessEnvironmentVariable("NAME", "bad\0value"));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("runtime")]
    [InlineData("session")]
    [InlineData("run")]
    public void ThrowIfInvalidExternalDeferrals_WhenHandoffIsInvalid_InfersCollectionNameAndRejects(string invalid)
    {
        var basis = RunResultTestData.Deferred();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        ImmutableArray<DeferredOperationRequest> requests = invalid switch
        {
            "default" => default,
            "empty" => [],
            "null" => [null!],
            "duplicate" => [basis, basis],
            "runtime" => [RunResultTestData.Deferred(kind: DeferralKind.ProviderSuspended, owner: DeferralContinuationOwner.RuntimeOperation, effects: DeferralEffectState.Started)],
            "session" => [basis, RunResultTestData.Deferred(2, session: new SessionId(id))],
            _ => [basis, RunResultTestData.Deferred(2, run: new RunId(id))],
        };
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidExternalDeferrals(requests));
        exception.ParamName.ShouldBe("requests");
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void ThrowIfInvalidExternalDeferrals_WhenAllowed_HandlesEmptyBoundaryAndExplicitParameterName()
    {
        ArgumentException.ThrowIfInvalidExternalDeferrals([], allowEmpty: true);
        ArgumentException.ThrowIfInvalidExternalDeferrals([RunResultTestData.Deferred()]);
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidExternalDeferrals([], paramName: "handoff")).ParamName.ShouldBe("handoff");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThrowIfNotSuccessfulRunOutcome_WhenCanonicalSuccessOrIdle_AcceptsCompletion(bool success)
    {
        AgentRunOutcome outcome = success ? new RunSucceeded() : new RunIdle();
        ArgumentException.ThrowIfNotSuccessfulRunOutcome(outcome);
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfSuccessfulRunOutcome(outcome)).ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void ThrowIfNotSuccessfulRunOutcome_WhenAgentRunCompletedHasIncompleteMessage_ThrowsArgumentException()
    {
        AgentRunOutcome outcome = new AgentRunCompleted(AssistantMessage(MessageState.Interrupted));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotSuccessfulRunOutcome(outcome));
        exception.ParamName.ShouldBe(nameof(outcome));
    }

    [Fact]
    public void ThrowIfNotSuccessfulRunOutcome_WhenAgentRunCompletedHasCompleteMessage_DoesNotThrow()
    {
        AgentRunOutcome outcome = new AgentRunCompleted(AssistantMessage(MessageState.Complete));
        Should.NotThrow(() => ArgumentException.ThrowIfNotSuccessfulRunOutcome(outcome));
    }

    private static AssistantMessage AssistantMessage(MessageState state) => new(
        new MessageId(Guid.NewGuid()),
        new AgentId(Guid.NewGuid()),
        new SessionId(Guid.NewGuid()),
        null,
        new BranchId(Guid.NewGuid()),
        new RunId(Guid.NewGuid()),
        new TurnId(Guid.NewGuid()),
        DateTimeOffset.UnixEpoch,
        state,
        [new TextPart("done", TextSemantics.Plain, ExtensionData.Empty)],
        new AssistantResponseMetadata(
            new ModelRequestId(Guid.NewGuid()),
            new ProviderResponseIdentity(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("test"), new ModelId("test"), null, null, null),
            NormalizedStopReason.Completed,
            null,
            ModelUsage.NotReported,
            ExtensionData.Empty),
        ExtensionData.Empty);

    [Fact]
    public void ThrowIfIssuerMismatch_WhenIssuerMatches_DoesNotThrow()
    {
        var evidence = Evidence("issuer");
        Should.NotThrow(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("issuer")));
    }

    [Fact]
    public void ThrowIfIssuerMismatch_WhenIssuerDiffers_ThrowsWithInferredParameterName()
    {
        var evidence = Evidence("first");
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("second")));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfIssuerMismatch_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        AuthenticationEvidence evidence = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("issuer")));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenAllAncestorsShareTenant_DoesNotRequireSamePrincipal()
    {
        ImmutableArray<DelegationIdentityLink> chain = [Link("tenant", "parent")];
        Should.NotThrow(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenAncestorUsesAnotherTenant_ThrowsWithInferredParameterName()
    {
        ImmutableArray<DelegationIdentityLink> chain = [Link("other", "parent")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));
        exception.ParamName.ShouldBe("chain");
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenChainIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<DelegationIdentityLink> chain = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));
        exception.ParamName.ShouldBe("chain");
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenClaimsAreNarrower_DoesNotThrow()
    {
        var claim = Claim("reader");
        ImmutableArray<IdentityClaim> requested = [claim];
        ImmutableArray<IdentityClaim> parent = [claim, Claim("writer")];
        Should.NotThrow(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenClaimWouldBroaden_ThrowsWithInferredParameterName()
    {
        ImmutableArray<IdentityClaim> requested = [Claim("admin")];
        ImmutableArray<IdentityClaim> parent = [Claim("reader")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));
        exception.ParamName.ShouldBe("requested");
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenRequestedClaimsAreDefault_ThrowsArgumentException()
    {
        ImmutableArray<IdentityClaim> requested = default;
        ImmutableArray<IdentityClaim> parent = [Claim("reader")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));
        exception.ParamName.ShouldBe("requested");
    }

    private static AuthenticationEvidence Evidence(string issuer) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));
    private static DelegationIdentityLink Link(string tenant, string principal) => new(new DelegationId(Guid.NewGuid()), new TenantId(tenant), new PrincipalId(principal), new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("evidence"), new IdentityVersion(1), DateTimeOffset.UnixEpoch, [Claim("reader")], IdentityAssuranceLevel.Strong);
    private static IdentityClaim Claim(string value) => new(new IdentityIssuerId("issuer"), "role", value, IdentityClaimValueKind.Text);
    [Fact]
    public void ThrowIfNotEqual_WhenValuesAreEqualOrNull_DoesNotThrow()
    {
        string? nullValue = null;
        var actual = new ContentHash("sha256:same");
        var reconstructed = new ContentHash("sha256:same");
        Should.NotThrow(() => ArgumentException.ThrowIfNotEqual(nullValue, null));
        Should.NotThrow(() => ArgumentException.ThrowIfNotEqual(actual, reconstructed));
    }

    [Fact]
    public void ThrowIfNotEqual_WhenValuesDiffer_ThrowsExactArgumentExceptionWithInferredParamName()
    {
        var actual = new ContentHash("sha256:actual");
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotEqual(actual, new ContentHash("sha256:expected")));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void ThrowIfNotEqual_WhenValuesDifferAndParamNameIsExplicit_UsesExplicitParamName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotEqual(1, 2, "evidence"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfInvalidIntentReceipt_WhenReceiptIsNull_AcceptsEveryDefinedStatus()
    {
        foreach (var status in Enum.GetValues<GrantConsumptionStatus>())
        {
            Should.NotThrow(() => ArgumentException.ThrowIfInvalidIntentReceipt(status, null));
        }
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Consumed)]
    [InlineData(GrantConsumptionStatus.Reconciled)]
    public void ThrowIfInvalidIntentReceipt_WhenStatusCarriesAuthorityEvidence_DoesNotThrow(GrantConsumptionStatus status)
    {
        var receipt = Receipt();
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidIntentReceipt(status, receipt));
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Unknown)]
    [InlineData(GrantConsumptionStatus.Tampered)]
    [InlineData(GrantConsumptionStatus.Mismatch)]
    [InlineData(GrantConsumptionStatus.Expired)]
    [InlineData(GrantConsumptionStatus.Revoked)]
    [InlineData(GrantConsumptionStatus.Exhausted)]
    public void ThrowIfInvalidIntentReceipt_WhenUnsuccessfulStatusCarriesReceipt_ThrowsExactArgumentException(GrantConsumptionStatus status)
    {
        var receipt = Receipt();
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidIntentReceipt(status, receipt));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("receipt");
    }

    [Fact]
    public void ThrowIfInvalidIntentReceipt_WhenParamNameIsExplicit_UsesExplicitParamName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidIntentReceipt(GrantConsumptionStatus.Unknown, Receipt(), "intentReceipt"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("intentReceipt");
    }

    private static SecurityEnforcementIntentReceipt Receipt()
    {
        var scope = new SecurityAuthorizationScope(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var enforcement = new SecurityEnforcementRequest(scope, identity, new ComponentId("component"), SecurityOperationKind.StateRead, SecurityEffect.Observe, [new ProtectedResource(ProtectedResourceKind.ApplicationState, "resource")], new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));
        return new SecurityEnforcementIntentReceipt(new SecurityEnforcementIntentId(Guid.NewGuid()), new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), enforcement, null, new ContentHash("sha256:effect"), DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ThrowIfNotClosedType_WhenTypeIsValid_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfNotClosedType(typeof(string)));
    [Fact]
    public void ThrowIfNotClosedType_WhenTypeIsNullOrOpenGeneric_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotClosedType(nullType!));
        var openException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotClosedType(typeof(List<>)));
        nullException.ParamName.ShouldBe("nullType");
        openException.ParamName.ShouldBe("typeof(List<>)");
    }

    [Fact]
    public void ThrowIfNotConcreteClosedType_WhenTypeIsValid_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfNotConcreteClosedType(typeof(Implementation)));
    [Fact]
    public void ThrowIfNotConcreteClosedType_WhenTypeIsNullOrInterface_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotConcreteClosedType(nullType!));
        var interfaceException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotConcreteClosedType(typeof(IDisposable)));
        nullException.ParamName.ShouldBe("nullType");
        interfaceException.ParamName.ShouldBe("typeof(IDisposable)");
    }

    [Fact]
    public void ThrowIfNotComponentContractType_WhenTypeIsValid_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfNotComponentContractType(typeof(IContract)));
    [Fact]
    public void ThrowIfNotComponentContractType_WhenTypeIsNullOrValueType_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotComponentContractType(nullType!));
        var valueException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotComponentContractType(typeof(int)));
        nullException.ParamName.ShouldBe("nullType");
        valueException.ParamName.ShouldBe("typeof(int)");
    }

    [Fact]
    public void ThrowIfNotComponentImplementationType_WhenTypeIsValid_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfNotComponentImplementationType(typeof(Implementation)));
    [Fact]
    public void ThrowIfNotComponentImplementationType_WhenTypeIsNullOrVoid_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotComponentImplementationType(nullType!));
        var voidException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotComponentImplementationType(typeof(void)));
        nullException.ParamName.ShouldBe("nullType");
        voidException.ParamName.ShouldBe("typeof(void)");
    }

    [Fact]
    public void ThrowIfNotAssignableTo_WhenTypesAreAssignable_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfNotAssignableTo(typeof(Implementation), typeof(IContract)));
    [Fact]
    public void ThrowIfNotAssignableTo_WhenEitherTypeIsNullOrImplementationIsWrong_ThrowsExactExpectedException()
    {
        Type? nullImplementation = null;
        Type? nullContract = null;
        var implementationException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotAssignableTo(nullImplementation!, typeof(IContract)));
        var contractException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotAssignableTo(typeof(Implementation), nullContract!));
        var mismatchException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotAssignableTo(typeof(string), typeof(IDisposable)));
        implementationException.ParamName.ShouldBe("nullImplementation");
        contractException.ParamName.ShouldBe("contractType");
        mismatchException.ParamName.ShouldBe("typeof(string)");
    }

    [Fact]
    public void ThrowIfNotDisposalContract_WhenContractIsDisposable_DoesNotThrow()
    {
        Should.NotThrow(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IDisposable)));
        Should.NotThrow(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IAsyncDisposable)));
    }

    [Fact]
    public void ThrowIfNotDisposalContract_WhenContractIsNullOrWrong_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotDisposalContract(nullType!));
        var wrongException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IContract)));
        nullException.ParamName.ShouldBe("nullType");
        wrongException.ParamName.ShouldBe("typeof(IContract)");
    }

    private static TException ShouldThrowExactly<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private interface IContract;
    private sealed class Implementation: IContract;
    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenSessionlessBeforeRun_DoesNotThrow()
    {
        var authorization = Authorization(new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), sessionId: null);
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));
    }

    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenNull_ThrowsExactArgumentNullExceptionWithInferredName()
    {
        SecurityAuthorizationContext authorization = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenScopeIsNotSessionlessBeforeRun_ThrowsExactArgumentException(int invalidCase)
    {
        var authorization = InvalidAuthorization(invalidCase);
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void ThrowIfInvalidSessionCreationAuthorization_WhenParamNameIsExplicit_UsesExplicitName()
    {
        var authorization = InvalidAuthorization(0);
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionCreationAuthorization(authorization, "captured"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("captured");
    }

    private static SecurityAuthorizationContext InvalidAuthorization(int invalidCase) => invalidCase switch
    {
        0 => Authorization(new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), new SessionId(Guid.NewGuid())),
        1 => Authorization(new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), sessionId: null),
        _ => Authorization(new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid())), sessionId: null),
    };
    private static SecurityAuthorizationContext Authorization(OperationCorrelation correlation, SessionId? sessionId)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityAuthorizationContext(new SecurityProfileKey("default"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayIsDefault_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        ImmutableArray<BudgetLimit> limits = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayContainsNull_ThrowsExactArgumentExceptionWithExplicitParameterName()
    {
        ImmutableArray<BudgetLimit> limits = [null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits, "scopeLimits"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("scopeLimits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenDimensionRepeats_ThrowsExactArgumentException()
    {
        var dimension = new BudgetDimension("tests.requests");
        ImmutableArray<BudgetLimit> limits = [new BudgetLimit(dimension, 0m, new BudgetUnit("requests"), BudgetLimitKind.Hard), new BudgetLimit(dimension, 1m, new BudgetUnit("requests"), BudgetLimitKind.Soft),];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayIsEmpty_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits([]));

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenDistinctZeroBoundaryLimitsAreValid_DoesNotThrow()
    {
        ImmutableArray<BudgetLimit> limits = [new BudgetLimit(new BudgetDimension("tests.requests"), 0m, new BudgetUnit("requests"), BudgetLimitKind.Hard), new BudgetLimit(new BudgetDimension("tests.tokens"), 0m, new BudgetUnit("tokens"), BudgetLimitKind.Soft),];
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenValid_DoesNotThrow()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId);
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenDefault_ThrowsWithInferredParameterName()
    {
        ImmutableArray<BudgetReservationRequest> requests = default;
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenMemberIsNull_ThrowsWithInferredParameterName()
    {
        ImmutableArray<BudgetReservationRequest> requests = [null!];
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenScopeDiffers_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var requests = CreateRequests(new BudgetScopeId(Guid.NewGuid()), new OperationId(Guid.NewGuid()));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenOperationDiffers_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, new OperationId(Guid.NewGuid())).SetItem(1, CreateRequest(scopeId, new OperationId(Guid.NewGuid()), "two"));
        _ = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenItemKeyRepeats_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId).SetItem(1, CreateRequest(scopeId, operationId, "one"));
        _ = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenDimensionUsesDifferentUnits_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId).SetItem(1, new BudgetReservationRequest(scopeId, new BudgetDimension("tests.calls"), 1m, new BudgetUnit("other"), operationId, null, new IdempotencyKey("two")));
        _ = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    private static ImmutableArray<BudgetReservationRequest> CreateRequests(BudgetScopeId scopeId, OperationId operationId) => [CreateRequest(scopeId, operationId, "one"), CreateRequest(scopeId, operationId, "two")];
    private static BudgetReservationRequest CreateRequest(BudgetScopeId scopeId, OperationId operationId, string itemKey) => new(scopeId, new BudgetDimension("tests.calls"), 1m, new BudgetUnit("calls"), operationId, null, new IdempotencyKey(itemKey));
    [Fact]
    public void ValidateAuthorization_WhenAuthorizationNull_ThrowsBeforeDereference()
    {
        var fixture = Fixture.Create();
        var exception = Should.Throw<ArgumentNullException>(() => AcceptedToolCall.ValidateAuthorization(fixture.AgentId, fixture.SessionId, fixture.RunId, fixture.TurnId, fixture.OperationId, null!));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void ValidateIdempotency_WhenEffectsNull_ThrowsBeforeDereference()
    {
        var exception = Should.Throw<ArgumentNullException>(() => AcceptedToolCall.ValidateIdempotency(null!, null));
        exception.ParamName.ShouldBe("effects");
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
            Identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
            Authorization = TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);
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

    [Fact]
    public void ThrowIfDuplicateSecurityAuditEventKind_WhenValuesAreValidOrEmpty_DoesNotThrow()
    {
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind([]));
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind([SecurityAuditEventKind.Decision]));
    }

    [Fact]
    public void ThrowIfDuplicateSecurityAuditEventKind_WhenValuesContainDuplicate_ReportsInferredAndExplicitParameterNames()
    {
        ImmutableArray<SecurityAuditEventKind> values = [SecurityAuditEventKind.Decision, SecurityAuditEventKind.Decision];
        var inferred = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(values));
        var explicitName = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(values, "eventKinds"));
        inferred.GetType().ShouldBe(typeof(ArgumentException));
        inferred.ParamName.ShouldBe(nameof(values));
        explicitName.GetType().ShouldBe(typeof(ArgumentException));
        explicitName.ParamName.ShouldBe("eventKinds");
    }

    [Fact]
    public void ThrowIfDuplicateSecurityAuditEventKind_WhenValuesAreDefaultOrUndefined_ThrowsWithExactParameterNames()
    {
        ImmutableArray<SecurityAuditEventKind> uninitialized = default;
        ImmutableArray<SecurityAuditEventKind> undefined = [(SecurityAuditEventKind) 99];
        var defaultException = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(uninitialized));
        var undefinedException = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(undefined));
        defaultException.GetType().ShouldBe(typeof(ArgumentException));
        defaultException.ParamName.ShouldBe(nameof(uninitialized));
        undefinedException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        undefinedException.ParamName.ShouldBe(nameof(undefined));
    }

    [Fact]
    public void ThrowIfDuplicateQuestionOptionIds_WhenIdsUnique_DoesNotThrow()
    {
        ImmutableArray<HumanQuestionOption> options = [new(new QuestionOptionId("one"), "One", "First."), new(new QuestionOptionId("two"), "Two", "Second."),];
        var action = () => ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);
        action.ShouldNotThrow();
    }

    [Fact]
    public void ThrowIfDuplicateQuestionOptionIds_WhenIdsDuplicate_InfersParameterName()
    {
        var option = new HumanQuestionOption(new QuestionOptionId("same"), "One", "First.");
        ImmutableArray<HumanQuestionOption> options = [option, option];
        var action = () => ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(options));
    }

    [Fact]
    public void ThrowIfDuplicatePlanItemIds_WhenIdsDuplicate_InfersParameterName()
    {
        var item = new WorkPlanItem(new PlanItemId("same"), "Work.", PlanItemStatus.Pending);
        ImmutableArray<WorkPlanItem> items = [item, item];
        var action = () => ArgumentException.ThrowIfDuplicatePlanItemIds(items);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(items));
    }

    [Fact]
    public void ThrowIfMultipleInProgressPlanItems_WhenOneActive_DoesNotThrow()
    {
        ImmutableArray<WorkPlanItem> items = [new(new PlanItemId("one"), "First.", PlanItemStatus.InProgress), new(new PlanItemId("two"), "Second.", PlanItemStatus.Pending),];
        var action = () => ArgumentException.ThrowIfMultipleInProgressPlanItems(items);
        action.ShouldNotThrow();
    }

    [Fact]
    public void ThrowIfMultipleInProgressPlanItems_WhenTwoActive_InfersParameterName()
    {
        ImmutableArray<WorkPlanItem> items = [new(new PlanItemId("one"), "First.", PlanItemStatus.InProgress), new(new PlanItemId("two"), "Second.", PlanItemStatus.InProgress),];
        var action = () => ArgumentException.ThrowIfMultipleInProgressPlanItems(items);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(items));
    }

    private static readonly JsonSchemaDialectId _dialect = new("urn:test:dialect");
    [Fact]
    public void ThrowIfUnsupportedOutputSchemaDialect_WhenUnsupported_UsesInferredParameterName()
    {
        var dialect = new JsonSchemaDialectId("urn:test:unsupported");
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfUnsupportedOutputSchemaDialect(Profile(), dialect));
        exception.ParamName.ShouldBe("dialect");
        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfUnsupportedOutputSchemaDialect(null!, _dialect)).ParamName.ShouldBe("profile");
    }

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
    [Fact]
    public void ThrowIfInvalidWebResultUri_WhenCredentialFreeHttps_DoesNotThrow()
    {
        var uri = new Uri("https://example.com/page");
        var action = () => ArgumentException.ThrowIfInvalidWebResultUri(uri);
        action.ShouldNotThrow();
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://user:secret@example.com/")]
    [InlineData("relative")]
    public void ThrowIfInvalidWebResultUri_WhenInvalid_InfersParameterName(string value)
    {
        var uri = new Uri(value, UriKind.RelativeOrAbsolute);
        var action = () => ArgumentException.ThrowIfInvalidWebResultUri(uri);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(uri));
    }

    [Fact]
    public void ThrowIfNotNetworkEndpointResource_WhenKindDiffers_InfersParameterName()
    {
        var resource = new ProtectedResource(ProtectedResourceKind.ApplicationState, "state");
        var action = () => ArgumentException.ThrowIfNotNetworkEndpointResource(resource);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(resource));
    }

    [Fact]
    public void ThrowIfNotNetworkEndpointResource_WhenKindIsNetworkEndpoint_DoesNotThrow()
    {
        var resource = new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "endpoint");
        Should.NotThrow(() => ArgumentException.ThrowIfNotNetworkEndpointResource(resource));
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenValuesAreValid_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1, Version2], Version1));
    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenWriteVersionIsDefault_ReportsWriteVersionParameterName()
    {
        SchemaVersion writeVersion = default;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1], writeVersion));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("writeVersion");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenVersionsAreEmpty_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = [];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenVersionsAreDefault_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenAValueIsDefault_ReportsExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1, default], Version1, "versions"));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("versions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenAValueIsDuplicate_ReportsExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions([Version1, Version1], Version1, "versions"));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("versions");
    }

    [Fact]
    public void ThrowIfInvalidSessionEntryCodecReadableVersions_WhenWriteVersionIsOmitted_ReportsInferredParameterName()
    {
        ImmutableArray<SchemaVersion> readableVersions = [Version2];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionEntryCodecReadableVersions(readableVersions, Version1));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("readableVersions");
    }

    private static readonly SchemaVersion Version1 = new("1");
    private static readonly SchemaVersion Version2 = new("2");
    [Fact]
    public void ThrowIfSessionContextNotInRun_WhenContextIsValid_DoesNotThrow()
    {
        var context = Context(inRun: true, laneBound: true);
        Should.NotThrow(() => ArgumentException.ThrowIfSessionContextNotInRun(context));
        Should.NotThrow(() => ArgumentException.ThrowIfSessionContextNotInRun(context with { }));
    }

    [Fact]
    public void ThrowIfSessionContextNotInRun_WhenContextIsNull_ThrowsInferredArgumentNullException()
    {
        SessionOperationContext context = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfSessionContextNotInRun(context));
        exception.ParamName.ShouldBe("context");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void ThrowIfSessionContextNotInRun_WhenContextIsInvalid_ThrowsExactArgumentException(bool inRun, bool laneBound)
    {
        var context = Context(inRun, laneBound);
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfSessionContextNotInRun(context, "candidate"));
        exception.ParamName.ShouldBe("candidate");
    }

    private static SessionOperationContext Context(bool inRun, bool laneBound)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        OperationCorrelation correlation = inRun ? new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null) : new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SessionOperationContext(agentId, sessionId, laneBound ? new ExecutionLaneId(Guid.NewGuid()) : null, correlation, identity, TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity));
    }

    [Fact]
    public void ThrowIfInvalidSessionDirectoryWriteBinding_WhenLocationTenantDiffers_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        var context = ContextSessionRoutingContracts();
        var location = Location(tenantId: new TenantId("other"));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionDirectoryWriteBinding(context, location));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(location));
    }

    [Fact]
    public void ThrowIfInvalidSessionDirectoryCreationBinding_WhenLocationTenantDiffers_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        var location = Location(tenantId: new TenantId("other"));
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSessionDirectoryCreationBinding(CreateRequestSessionRoutingContracts(), location));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(location));
    }

    private static SessionOperationContext ContextSessionRoutingContracts() => new(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")), executionLaneId: null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), AuthorizationSessionRoutingContracts());
    private static SessionCreateRequest CreateRequestSessionRoutingContracts() => new(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), CreationAuthorization(), conversationId: null, new IdempotencyKey("create"), ExtensionData.Empty);
    private static SecurityAuthorizationContext AuthorizationSessionRoutingContracts()
    {
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }

    private static SecurityAuthorizationContext CreationAuthorization()
    {
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, null, correlation), identity);
    }

    private static SessionLocation Location(TenantId? tenantId = null) => new(new SessionAddress(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"))), tenantId ?? new TenantId("tenant"), new SessionStoreKey("store"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));
    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenEvidenceExists_DoesNotThrow()
    {
        ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([Hold()], []);
        ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], [LimitFailure()]);
    }

    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenEmpty_UsesExplicitParameterName() => Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], [], "blockers")).ParamName.ShouldBe("blockers");

    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenArrayIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers(default, [])).ParamName.ShouldBe("currentOverruns");
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], default)).ParamName.ShouldBe("hardLimitFailures");
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenBoundaryNarrowsAddress_DoesNotThrow()
    {
        var tenant = new TenantId("tenant");
        var principal = new PrincipalId("principal");
        var agent = new AgentId(Guid.NewGuid());
        var charged = new BudgetScopeAddress(tenant, principal, agent, new SessionId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new OperationId(Guid.NewGuid()));
        ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(new BudgetScopeAddress(tenant, principal, agent, null, null, null), charged);
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenMismatch_UsesExplicitParameterName()
    {
        var agent = new AgentId(Guid.NewGuid());
        var boundary = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), agent, null, null, null);
        var charged = new BudgetScopeAddress(new TenantId("other"), new PrincipalId("principal"), agent, null, null, null);
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(boundary, charged, "chargedScope")).ParamName.ShouldBe("chargedScope");
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenNullOrInferredMismatch_UsesExactParameterName()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(null!, address)).ParamName.ShouldBe("boundary");
        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(address, null!)).ParamName.ShouldBe("charged");
        var changed = new BudgetScopeAddress(new TenantId("other"), address.PrincipalId, address.AgentId, null, null, null);
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(address, changed)).ParamName.ShouldBe("changed");
    }

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static BudgetDimension Dimension() => new("tests.overrun");
    private static BudgetUnit Unit() => new("count");
    private static BudgetOverrunHold Hold() => new(HoldReference(), Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
    private static BudgetLimitFailure LimitFailure() => new(HoldReference().Boundary.Id, Dimension(), BudgetLimitKind.Hard, 10, 11, 0, Unit(), "limit");
    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsValid_DoesNotThrow()
    {
        var request = Request(Scope().Id, amount: decimal.One);
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsNull_UsesInferredParameterName()
    {
        BudgetReservationRequest request = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsNull_UsesExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(null!, "candidate"));
        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenScopeIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = Request(Scope().Id) with
        {
            ScopeId = default
        };
        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenDimensionIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with
        {
            Dimension = default
        };
        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenAmountIsNotPositive_ThrowsArgumentOutOfRangeException(int amount)
    {
        var request = Request(Scope().Id) with
        {
            Amount = amount
        };
        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenUnitIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with
        {
            Unit = default
        };
        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenOperationIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = Request(Scope().Id) with
        {
            OperationId = default
        };
        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenIdempotencyKeyIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with
        {
            IdempotencyKey = default
        };
        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreValid_DoesNotThrow()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "first"), ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "second")];
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreDefault_UsesInferredParameterName()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreEmpty_UsesExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts([], "candidate"));
        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenScopesDiffer_ThrowsArgumentException()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(Scope(), ReservationIdBudgetLedgerContracts(), "first"), ReceiptBudgetLedgerContracts(Scope(), ReservationIdBudgetLedgerContracts(), "second")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReservationIdentityRepeats_ThrowsArgumentException()
    {
        var scope = Scope();
        var reservationId = ReservationIdBudgetLedgerContracts();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(scope, reservationId, "first"), ReceiptBudgetLedgerContracts(scope, reservationId, "second")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenItemKeyRepeats_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "same"), ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "same")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenOriginalOperationsDiffer_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "first", operationId: OperationId()), ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "second", operationId: OperationId())];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenDimensionUnitsDiffer_ThrowsArgumentException()
    {
        var scope = Scope();
        var operationId = OperationId();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "first", dimension: "shared", unit: "requests", operationId: operationId), ReceiptBudgetLedgerContracts(scope, ReservationIdBudgetLedgerContracts(), "second", dimension: "shared", unit: "tokens", operationId: operationId)];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenPageIsValidWithoutContinuation_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), [], null));
    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenPageIsValidWithContinuation_DoesNotThrow()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationIdBudgetLedgerContracts("00000000-0000-0000-0000-000000000001");
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, watermark, id);
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, watermark, items, next));
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        BudgetLedgerScopeReference scope = null!;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), [], null));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenWatermarkIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), default, [], null));
        exception.ParamName.ShouldBe("watermark");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsAreDefault_UsesInferredParameterName()
    {
        ImmutableArray<BudgetUnresolvedReservation> items = default;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), items, null));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<BudgetUnresolvedReservation> items = [null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), items, null));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemScopeDiffers_UsesExplicitParameterName()
    {
        var scope = Scope();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(Scope(), ReservationIdBudgetLedgerContracts(), "first")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null, "candidate"));
        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsAreDescending_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, ReservationIdBudgetLedgerContracts("00000000-0000-0000-0000-000000000002"), "second"), Unresolved(scope, ReservationIdBudgetLedgerContracts("00000000-0000-0000-0000-000000000001"), "first")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsRepeatIdentity_ThrowsArgumentException()
    {
        var scope = Scope();
        var id = ReservationIdBudgetLedgerContracts("00000000-0000-0000-0000-000000000001");
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first"), Unresolved(scope, id, "second")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationScopeDiffers_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationIdBudgetLedgerContracts();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(Scope(), watermark, id);
        AssertInvalidPage(scope, watermark, items, next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationWatermarkDiffers_ThrowsArgumentException()
    {
        var scope = Scope();
        var id = ReservationIdBudgetLedgerContracts();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, new BudgetLedgerWatermark(2), id);
        AssertInvalidPage(scope, new BudgetLedgerWatermark(1), items, next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenEmptyPageHasContinuation_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var next = new BudgetReservationCursor(scope, watermark, ReservationIdBudgetLedgerContracts());
        AssertInvalidPage(scope, watermark, [], next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationDoesNotAnchorLastItem_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationIdBudgetLedgerContracts();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, watermark, ReservationIdBudgetLedgerContracts());
        AssertInvalidPage(scope, watermark, items, next);
    }

    private static void AssertInvalidReservationRequest<TException>(BudgetReservationRequest request)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));
        exception.ParamName.ShouldBe("request");
    }

    private static void AssertInvalidPage(BudgetLedgerScopeReference scope, BudgetLedgerWatermark watermark, ImmutableArray<BudgetUnresolvedReservation> items, BudgetReservationCursor next)
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, watermark, items, next));
        exception.ParamName.ShouldBe("items");
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenBothArraysAreEmpty_ThrowsInferredParameterName()
    {
        ImmutableArray<BudgetOverrunHold> currentOverruns = [];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers(currentOverruns, []));
        exception.ParamName.ShouldBe("currentOverruns");
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationIdBudgetLedgerContracts() => new(Guid.NewGuid());
    private static BudgetReservationId ReservationIdBudgetLedgerContracts(string value) => new(Guid.Parse(value));
    private static OperationId OperationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
    private static BudgetLedgerReservationReceipt ReceiptBudgetLedgerContracts(BudgetLedgerScopeReference scope, BudgetReservationId reservationId, string idempotencyKey, string dimension = "tests.requests", string unit = "requests", OperationId? operationId = null) => new(new BudgetLedgerReservationReference(scope, reservationId), Request(scope.Id, dimension: dimension, unit: unit, idempotencyKey: idempotencyKey, operationId: operationId), new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
    private static BudgetUnresolvedReservation Unresolved(BudgetLedgerScopeReference scope, BudgetReservationId reservationId, string idempotencyKey) => new(ReceiptBudgetLedgerContracts(scope, reservationId, idempotencyKey), DateTimeOffset.UnixEpoch.AddTicks(-1));
    private static AdmittedInput Admitted(AgentInput original, AgentInput effective, SessionSequence? promoted = null) => new(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), original, effective, Manifest(), DateTimeOffset.UnixEpoch, promoted);
    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputPreprocessingManifest Manifest() => new(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1"));
    private static ExecutionIdentity Identity() => TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());
    private static SecurityAuthorizationContext AuthorizationInputAdmissionContracts(ExecutionIdentity identity, AgentId agentId, SessionId sessionId, OperationCorrelation correlation) => new(new SecurityProfileKey("profile"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("safe")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    private static TException ShouldThrowExactlyInputAdmissionContracts<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    [Fact]
    public void InputPromotionGuards_WhenCalledThroughArgumentExceptionType_ValidateExactInputEvidence()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var authorization = AuthorizationInputAdmissionContracts(Identity(), Agent(), Session(), Operation());
        var validEligible = ImmutableArray.Create(Admitted(payload, payload));
        ImmutableArray<AdmissionId> defaultAdmissions = default;
        var otherPayload = Payload(2, InputDelivery.Steer);
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidPromotionEligibleInputs(validEligible, Agent(), Session(), Lane(), new SessionSequence(1)));
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(Identity(), Agent(), Session(), Operation(), authorization));
        var admissionsException = ShouldThrowExactlyInputAdmissionContracts<ArgumentException>(() => ArgumentException.ThrowIfInvalidPromotionAdmissions(defaultAdmissions));
        var payloadException = ShouldThrowExactlyInputAdmissionContracts<ArgumentException>(() => ArgumentException.ThrowIfInvalidAdmittedInputPayloads(payload, otherPayload, Manifest()));
        admissionsException.ParamName.ShouldBe("defaultAdmissions");
        payloadException.ParamName.ShouldBe("otherPayload");
    }

    [Fact]
    public void InputPromotionGuards_WhenCalledDirectly_ValidateBoundaryAndAddressParameters()
    {
        var authorization = AuthorizationInputAdmissionContracts(Identity(), Agent(), Session(), Operation());
        var admissionAddressException = ShouldThrowExactlyInputAdmissionContracts<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(Identity(), default, Session(), Operation(), authorization));
        var promotionAddressException = ShouldThrowExactlyInputAdmissionContracts<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidInputPromotionAuthorization(Identity(), Agent(), default, Operation(), authorization));
        var operationException = ShouldThrowExactlyInputAdmissionContracts<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidInputPromotionAuthorization(Identity(), Agent(), Session(), null!, authorization));
        var boundaryException = ShouldThrowExactlyInputAdmissionContracts<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidPromotionTurnBoundary((PromotionBoundary) 42, null, NextTurn()));
        var previousTurnException = ShouldThrowExactlyInputAdmissionContracts<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidPromotionTurnBoundary(PromotionBoundary.AfterContinuationCheckpoint, default(TurnId), NextTurn()));
        var targetTurnException = ShouldThrowExactlyInputAdmissionContracts<ArgumentOutOfRangeException>(() => ArgumentException.ThrowIfInvalidPromotionTurnBoundary(PromotionBoundary.AfterContinuationCheckpoint, null, default));
        admissionAddressException.ParamName.ShouldBe("agentId");
        promotionAddressException.ParamName.ShouldBe("sessionId");
        operationException.ParamName.ShouldBe("expectedOperation");
        boundaryException.ParamName.ShouldBe("boundary");
        previousTurnException.ParamName.ShouldBe("previousTurnId");
        targetTurnException.ParamName.ShouldBe("targetTurnId");
    }

    [Fact]
    public void ThrowIfPathNotRooted_WhenPathIsRelative_ThrowsExactParameter()
    {
        const string path = "relative/toolchain";

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfPathNotRooted(path));

        exception.ParamName.ShouldBe(nameof(path));
    }

    [Fact]
    public void ThrowIfPathNotRooted_WhenPathIsAbsolute_DoesNotThrow() =>
        ArgumentException.ThrowIfPathNotRooted(Path.GetFullPath("toolchain"));

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenContainsNull_ThrowsArgumentException()
    {
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one"), null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenSourceIdIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<IAgentDefinitionSource> sources = [new FakeAgentDefinitionSource(default)];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenIdsRepeat_ThrowsArgumentException()
    {
        ImmutableArray<IAgentDefinitionSource> sources = [Source("dup"), Source("dup")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenIdsAreDistinct_DoesNotThrow()
    {
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one"), Source("two")];
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenContainsNull_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one"), null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));
        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenIdsRepeat_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("dup"), Snapshot("dup")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));
        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenIdsAreDistinct_DoesNotThrow()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one"), Snapshot("two")];
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSnapshotsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one"), null!];
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSourcesContainNull_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one")];
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one"), null!];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSourceIdIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one")];
        ImmutableArray<IAgentDefinitionSource> sources = [new FakeAgentDefinitionSource(default)];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSnapshotNamesUnregisteredSource_ThrowsArgumentException()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("unregistered")];
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one")];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenAllSnapshotsNameRegisteredSources_DoesNotThrow()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [Snapshot("one")];
        ImmutableArray<IAgentDefinitionSource> sources = [Source("one")];
        Should.NotThrow(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
    }

    private static FakeAgentDefinitionSource Source(string sourceId) => new(new AgentDefinitionSourceId(sourceId));
    private static AgentDefinitionSourceSnapshot Snapshot(string sourceId) => new(new AgentDefinitionSourceId(sourceId), new AgentDefinitionSourceVersion(1), 0, []);

    private sealed class FakeAgentDefinitionSource(AgentDefinitionSourceId sourceId): IAgentDefinitionSource
    {
        public AgentDefinitionSourceId SourceId { get; } = sourceId;

        public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
