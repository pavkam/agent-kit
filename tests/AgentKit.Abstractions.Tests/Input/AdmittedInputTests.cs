// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies AdmittedInput behavior and contracts.</summary>
public sealed class AdmittedInputTests
{
    [Fact]
    public void AdmittedInput_WhenEffectivePayloadChangesDelivery_ThrowsArgumentExceptionWithParamName()
    {
        var original = Payload(1, InputDelivery.Steer);
        var effective = Payload(1, InputDelivery.FollowUp);
        var exception = ShouldThrowExactly<ArgumentException>(() => Admitted(original, effective));
        exception.ParamName.ShouldBe("effectivePayload");
    }

    [Fact]
    public void AdmittedInput_WhenPromotionDoesNotFollowAdmission_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => Admitted(payload, payload, new SessionSequence(1)));
        exception.ParamName.ShouldBe("promotedSequence");
    }

    private static AdmittedInput Admitted(AgentInput original, AgentInput effective, SessionSequence? promoted = null) => new(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), original, effective, Manifest(), DateTimeOffset.UnixEpoch, promoted);
    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputPreprocessingManifest Manifest() => new(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));

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
        AgentInput? nullPayload = null;
        InputPreprocessingManifest? nullManifest = null;
        yield return new object?[]
        {
            () => new AdmittedInput(default, Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentOutOfRangeException),
            "admissionId"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), default, Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), default, Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), default, Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), nullIdentity!, new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentNullException),
            "identity"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(0), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentOutOfRangeException),
            "admittedSequence"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), nullPayload!, Payload(1, InputDelivery.Steer), Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentNullException),
            "originalPayload"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), nullPayload!, Manifest(), DateTimeOffset.UnixEpoch),
            typeof(ArgumentNullException),
            "effectivePayload"
        };
        yield return new object?[]
        {
            () => new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), nullManifest!, DateTimeOffset.UnixEpoch),
            typeof(ArgumentNullException),
            "preprocessing"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<AdmittedInput> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
