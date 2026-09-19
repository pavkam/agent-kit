// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>Verifies <see cref="ModelExecutionRequest"/>.</summary>
public sealed class ModelExecutionRequestTests
{
    [Theory]
    [InlineData(0, "operation")]
    [InlineData(1, "selection")]
    [InlineData(2, "context")]
    [InlineData(3, "retryPolicy")]
    public void Constructor_WhenRequiredReferenceIsNull_ThrowsArgumentNullException(int invalidMember, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelExecutionRequest(
            invalidMember == 0 ? null! : Operation(),
            invalidMember == 1 ? null! : ProvidersTestData.Decision(),
            invalidMember == 2 ? null! : Context(),
            budget: null,
            hooks: null,
            invalidMember == 3 ? null! : new ProviderRetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero)));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void Constructor_WhenBudgetAndHooksAreAbsent_CapturesTheRequest()
    {
        var operation = Operation();
        var selection = ProvidersTestData.Decision();
        var context = Context();
        var policy = new ProviderRetryPolicy(2, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1));
        var request = new ModelExecutionRequest(operation, selection, context, budget: null, hooks: null, policy);
        request.Operation.ShouldBeSameAs(operation);
        request.Selection.ShouldBe(selection);
        request.Context.ShouldBe(context);
        request.Budget.ShouldBeNull();
        request.Hooks.ShouldBeNull();
        request.RetryPolicy.ShouldBe(policy);
    }

    private static ProtectedSemanticOperationContext Operation()
    {
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            admissionId: null);
        return new ProtectedSemanticOperationContext(
            agentId,
            sessionId: null,
            conversationId: null,
            identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, sessionId: null, correlation, identity));
    }

    private static LlmRequestContext Context() => new(
        new ModelRequestId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
        ProvidersTestData.Descriptor(),
        [],
        [],
        LlmToolChoice.None,
        new LlmRequestSettings(null, null, null, [], null, null, ExtensionData.Empty),
        ExtensionData.Empty);
}
