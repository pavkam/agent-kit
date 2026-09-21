// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="LiteralInstructionSource"/> boundary guards.</summary>
public sealed class LiteralInstructionSourceTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var message = InstructionTestData.SystemMessage("be helpful");
        var source = new LiteralInstructionSource(
            ContextTestData.Source(),
            ContextTrust.AgentDefinition,
            priority: 2,
            ContextScope.Agent,
            ContextEvaluationFrequency.OncePerModelRequest,
            [message]);

        source.Trust.ShouldBe(ContextTrust.AgentDefinition);
        source.Priority.ShouldBe(2);
        source.Messages.ShouldBe([message]);
    }

    [Fact]
    public void Constructor_WhenMessagesContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            new LiteralInstructionSource(
                ContextTestData.Source(),
                ContextTrust.AgentDefinition,
                0,
                ContextScope.Agent,
                ContextEvaluationFrequency.OncePerModelRequest,
                [null!])).ParamName.ShouldBe("messages");
    }
}
