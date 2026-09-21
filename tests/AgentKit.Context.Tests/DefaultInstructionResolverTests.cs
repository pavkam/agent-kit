// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

/// <summary>Verifies <see cref="DefaultInstructionResolver"/> behavior.</summary>
public sealed class DefaultInstructionResolverTests
{
    private readonly IInstructionResolver _resolver = ContextTestSupport.CreateResolver();

    [Fact]
    public async Task ResolveAsync_WhenSourcesAreEmpty_ReturnsEmptyMessages()
    {
        var request = new InstructionResolutionRequest([], new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), new ModelRequestId(Guid.NewGuid()));

        var result = await _resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InstructionResolutionResolved>().Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenHigherPrioritySourceExists_OrdersMessagesByPriority()
    {
        var low = TestFactory.SystemMessage("low");
        var high = TestFactory.SystemMessage("high");
        var request = new InstructionResolutionRequest(
            [CreateLiteral([low], priority: 0), CreateLiteral([high], priority: 5)],
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            new ModelRequestId(Guid.NewGuid()));

        var resolved = (InstructionResolutionResolved) await _resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        resolved.Messages.ShouldBe([high, low]);
    }

    [Fact]
    public async Task ResolveAsync_WhenInstructionIsUserRole_FailsClosed()
    {
        var request = new InstructionResolutionRequest(
            [CreateLiteral([TestFactory.UserMessage()], priority: 0)],
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            new ModelRequestId(Guid.NewGuid()));

        var result = await _resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InstructionResolutionFailed>().Failure.Kind.ShouldBe(ContextPreparationFailureKind.InvalidInstructionMessage);
    }

    private static LiteralInstructionSource CreateLiteral(ImmutableArray<AgentMessage> messages, int priority) =>
        new(
            new ContextSourceReference(
                new ContextSourceNamespace("agentkit.context.tests"),
                new ContextSourceKey($"source-{priority}"),
                new ContextSourceVersion("1")),
            ContextTrust.AgentDefinition,
            priority,
            ContextScope.Agent,
            ContextEvaluationFrequency.OncePerModelRequest,
            messages);
}
