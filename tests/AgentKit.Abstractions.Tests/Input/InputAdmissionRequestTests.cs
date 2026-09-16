// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputAdmissionRequest behavior and contracts.</summary>
public sealed class InputAdmissionRequestTests
{
    [Fact]
    public void InputAdmissionRequest_WhenAuthorizationDiffersByCorrelation_ThrowsArgumentExceptionWithParamName()
    {
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000001")), null);
        var authorization = Authorization(Identity(), Agent(), Session(), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("80000000-0000-0000-0000-000000000002")), null));
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), correlation, authorization, Payload(1, InputDelivery.Steer)));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void InputAdmissionRequest_WhenAuthorizationIdentityOrAddressDiffers_ThrowsArgumentExceptionWithParamName()
    {
        var differentIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var identityException = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), Authorization(differentIdentity, Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)));
        var addressException = ShouldThrowExactly<ArgumentException>(() => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), OtherSession(), Operation()), Payload(1, InputDelivery.Steer)));
        identityException.ParamName.ShouldBe("authorization");
        addressException.ParamName.ShouldBe("authorization");
    }

    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static SessionId OtherSession() => new(Guid.Parse("30000000-0000-0000-0000-000000000002"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());
    private static SecurityAuthorizationContext Authorization(ExecutionIdentity identity, AgentId agentId, SessionId sessionId, OperationCorrelation correlation) => new(new SecurityProfileKey("profile"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("safe")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        ExecutionIdentity? nullIdentity = null;
        SecurityAuthorizationContext? nullAuthorization = null;
        OperationCorrelation? nullCorrelation = null;
        AgentInput? nullInput = null;
        yield return new object?[]
        {
            () => new InputAdmissionRequest(default, Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), default, Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), Session(), default, Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), Session(), Lane(), nullIdentity!, Operation(), Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)),
            typeof(ArgumentNullException),
            "identity"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), nullCorrelation!, Authorization(Identity(), Agent(), Session(), Operation()), Payload(1, InputDelivery.Steer)),
            typeof(ArgumentNullException),
            "correlation"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), nullAuthorization!, Payload(1, InputDelivery.Steer)),
            typeof(ArgumentNullException),
            "authorization"
        };
        yield return new object?[]
        {
            () => new InputAdmissionRequest(Agent(), Session(), Lane(), Identity(), Operation(), Authorization(Identity(), Agent(), Session(), Operation()), nullInput!),
            typeof(ArgumentNullException),
            "input"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<InputAdmissionRequest> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var identity = Identity();
        var correlation = Operation();
        var authorization = Authorization(identity, Agent(), Session(), correlation);
        var input = Payload(1, InputDelivery.Steer);
        var version = new SessionVersion(1);
        var request = new InputAdmissionRequest(Agent(), Session(), Lane(), identity, correlation, authorization, input, version);
        request.AgentId.ShouldBe(Agent());
        request.SessionId.ShouldBe(Session());
        request.ExecutionLaneId.ShouldBe(Lane());
        request.Identity.ShouldBe(identity);
        request.Correlation.ShouldBe(correlation);
        request.Authorization.ShouldBe(authorization);
        request.Input.ShouldBe(input);
        request.ExpectedVersion.ShouldBe(version);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var identity = Identity();
        var correlation = Operation();
        var original = new InputAdmissionRequest(Agent(), Session(), Lane(), identity, correlation, Authorization(identity, Agent(), Session(), correlation), Payload(1, InputDelivery.Steer));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
