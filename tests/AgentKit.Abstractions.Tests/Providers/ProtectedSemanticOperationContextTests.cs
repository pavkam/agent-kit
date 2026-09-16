// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit.TestSupport;

public sealed class ProtectedSemanticOperationContextTests
{
    [Theory]
    [InlineData(0, "agentId")]
    [InlineData(1, "sessionId")]
    [InlineData(2, "conversationId")]
    public void Constructor_WhenAddressIdentityIsDefault_ThrowsExactArgumentOutOfRangeException(
        int invalidMember,
        string expectedParamName)
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, operation.SessionId, operation.Correlation, operation.Identity);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProtectedSemanticOperationContext(
            invalidMember == 0 ? default : operation.AgentId,
            invalidMember == 1 ? default : operation.SessionId,
            invalidMember == 2 ? new ConversationId() : null,
            operation.Identity,
            operation.Correlation,
            authorization));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Theory]
    [InlineData(0, "identity")]
    [InlineData(1, "correlation")]
    [InlineData(2, "authorization")]
    public void Constructor_WhenRequiredReferenceIsNull_ThrowsExactArgumentNullException(
        int invalidMember,
        string expectedParamName)
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, operation.SessionId, operation.Correlation, operation.Identity);

        var exception = Should.Throw<ArgumentNullException>(() => new ProtectedSemanticOperationContext(
            operation.AgentId,
            operation.SessionId,
            null,
            invalidMember == 0 ? null! : operation.Identity,
            invalidMember == 1 ? null! : operation.Correlation,
            invalidMember == 2 ? null! : authorization));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenAuthorizationExactlyMatchesCorrelation_CapturesImmutableEvidence(int correlationKind)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var conversationId = new ConversationId(Guid.NewGuid());
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = Correlation(correlationKind);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);

        var context = new ProtectedSemanticOperationContext(
            agentId, sessionId, conversationId, identity, correlation, authorization);

        context.AgentId.ShouldBe(agentId);
        context.SessionId.ShouldBe(sessionId);
        context.ConversationId.ShouldBe(conversationId);
        context.Identity.ShouldBeSameAs(identity);
        context.Correlation.ShouldBeSameAs(correlation);
        context.Authorization.ShouldBeSameAs(authorization);
        typeof(ProtectedSemanticOperationContext).GetProperties()
            .ShouldAllBe(static property => property.SetMethod == null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Constructor_WhenBeforeOrAfterRunOperationIsSessionless_PreservesTruthfulScope(int correlationKind)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Service);
        var correlation = Correlation(correlationKind);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId: null, correlation, identity);

        var context = new ProtectedSemanticOperationContext(
            agentId, sessionId: null, conversationId: null, identity, correlation, authorization);

        context.SessionId.ShouldBeNull();
        context.ConversationId.ShouldBeNull();
        context.Correlation.ShouldBeSameAs(correlation);
        context.Authorization.Scope.SessionId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenAuthorizationAgentDiffers_ThrowsBeforeConstruction()
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            new AgentId(Guid.NewGuid()), operation.SessionId, operation.Correlation, operation.Identity);

        var exception = Should.Throw<ArgumentException>(() => new ProtectedSemanticOperationContext(
            operation.AgentId, operation.SessionId, null, operation.Identity, operation.Correlation, authorization));

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationSessionDiffers_ThrowsExactArgumentException()
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, new SessionId(Guid.NewGuid()), operation.Correlation, operation.Identity);

        var exception = Should.Throw<ArgumentException>(() => new ProtectedSemanticOperationContext(
            operation.AgentId, operation.SessionId, null, operation.Identity, operation.Correlation, authorization));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationCorrelationDiffers_ThrowsExactArgumentException()
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, operation.SessionId,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), admissionId: null),
            operation.Identity);

        var exception = Should.Throw<ArgumentException>(() => new ProtectedSemanticOperationContext(
            operation.AgentId, operation.SessionId, null, operation.Identity, operation.Correlation, authorization));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationIdentityDiffers_ThrowsExactArgumentException()
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, operation.SessionId, operation.Correlation,
            TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("other"), ExecutionSubjectKind.Human));

        var exception = Should.Throw<ArgumentException>(() => new ProtectedSemanticOperationContext(
            operation.AgentId, operation.SessionId, null, operation.Identity, operation.Correlation, authorization));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    private static (AgentId AgentId, SessionId SessionId, ExecutionIdentity Identity, OperationCorrelation Correlation)
        Operation() =>
        (
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new InRunOperationCorrelation(
                new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()))
        );

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var operation = Operation();
        var authorization = TestSecurityEvidence.Authorization(
            operation.AgentId, operation.SessionId, operation.Correlation, operation.Identity);
        var original = new ProtectedSemanticOperationContext(
            operation.AgentId, operation.SessionId, null, operation.Identity, operation.Correlation, authorization);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static OperationCorrelation Correlation(int kind) => kind switch
    {
        0 => new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), new AdmissionId(Guid.NewGuid())),
        1 => new InRunOperationCorrelation(
            new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid())),
        2 => new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid())),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
