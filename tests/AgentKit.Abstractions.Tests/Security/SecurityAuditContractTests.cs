// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies that immutable security-audit contracts reject incomplete identities and undefined classifications.</summary>
public sealed class SecurityAuditContractTests
{
    [Fact]
    public void Constructor_WhenAuditRecordIdIsEmpty_ThrowsArgumentOutOfRangeExceptionForValue()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditRecordId(Guid.Empty));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenAuditRecordHasDefaultIdentityOrUndefinedClassification_ThrowsForTheOwningParameter()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Record(id: new SecurityAuditRecordId())).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(eventKind: (SecurityAuditEventKind) 99)).ParamName.ShouldBe("eventKind");
        Should.Throw<ArgumentOutOfRangeException>(() => Record(outcome: (SecurityAuditOutcome) 99)).ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Constructor_WhenAuditSinkRegistrationHasInvalidSupportOrDelivery_ThrowsForSupportedEventKindsOrDelivery()
    {
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration(default, SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration([], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditSinkRegistration(
            [(SecurityAuditEventKind) 99], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration(
            [SecurityAuditEventKind.Decision, SecurityAuditEventKind.Decision], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditSinkRegistration(
            [SecurityAuditEventKind.Decision], (SecurityAuditDelivery) 99, true)).ParamName.ShouldBe("delivery");
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

        var inferred = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(values));
        var explicitName = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(values, "eventKinds"));

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

        var defaultException = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(uninitialized));
        var undefinedException = Should.Throw<ArgumentOutOfRangeException>(() =>
            ArgumentException.ThrowIfDuplicateSecurityAuditEventKind(undefined));

        defaultException.GetType().ShouldBe(typeof(ArgumentException));
        defaultException.ParamName.ShouldBe(nameof(uninitialized));
        undefinedException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        undefinedException.ParamName.ShouldBe(nameof(undefined));
    }

    [Fact]
    public void Constructor_WhenAuditRecordHasNullScopeOrFields_ThrowsWithExactParameterNames()
    {
        var id = new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111"));
        var requestId = new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444"));
        var policyVersion = new SecurityPolicyVersion(1);

        Should.Throw<ArgumentNullException>(() => new SecurityAuditRecord(
            id, null!, requestId, null, null, SecurityAuditEventKind.Decision, SecurityAuditOutcome.Accepted,
            policyVersion, [], DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new SecurityAuditRecord(
            id, Scope(), requestId, null, null, SecurityAuditEventKind.Decision, SecurityAuditOutcome.Accepted,
            policyVersion, null!, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("fields");
    }

    [Fact]
    public void AuditResultAndRedactedValueFactories_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuditFailed(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditFailed(" ")).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentNullException>(() => new SecurityAuditUnavailable(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditUnavailable(" ")).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentNullException>(() => new SecurityAuditTimedOut(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditTimedOut(" ")).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentNullException>(() => RedactedAuditValue.FromPolicyId(default)).ParamName.ShouldBe("policyId");
        Should.Throw<ArgumentNullException>(() => RedactedAuditValue.FromComponentId(default)).ParamName.ShouldBe("componentId");
        Should.Throw<ArgumentOutOfRangeException>(() => RedactedAuditValue.FromOperationKind((SecurityOperationKind) 99)).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentOutOfRangeException>(() => RedactedAuditValue.FromEffect((SecurityEffect) 99)).ParamName.ShouldBe("effect");
    }

    private static SecurityAuditRecord Record(
        SecurityAuditRecordId? id = null,
        SecurityAuditEventKind? eventKind = null,
        SecurityAuditOutcome? outcome = null) => new(
        id ?? new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")),
        Scope(),
        new SecurityRequestId(Guid.Parse("a4444444-4444-4444-4444-444444444444")),
        null,
        null,
        eventKind ?? SecurityAuditEventKind.Decision,
        outcome ?? SecurityAuditOutcome.Accepted,
        new SecurityPolicyVersion(1),
        [],
        DateTimeOffset.UnixEpoch);

    private static SecurityAuthorizationScope Scope() => new(
        new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")),
        null,
        new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("a3333333-3333-3333-3333-333333333333")),
            null));
}
