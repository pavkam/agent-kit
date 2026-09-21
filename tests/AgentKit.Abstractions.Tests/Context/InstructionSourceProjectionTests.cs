// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="InstructionSourceProjection"/> behavior.</summary>
public sealed class InstructionSourceProjectionTests
{
    [Fact]
    public void FromLegacyMessages_WhenInstructionsAreEmpty_ReturnsEmptySources() =>
        InstructionSourceProjection.FromLegacyMessages([], new AgentDefinitionRevision(1)).ShouldBeEmpty();

    [Fact]
    public void ToMessages_WhenLiteralSourcePresent_ReturnsMessagesInOrder()
    {
        var first = InstructionTestData.SystemMessage("first");
        var second = InstructionTestData.SystemMessage("second");
        var sources = ImmutableArray<InstructionSource>.Empty.Add(
            new LiteralInstructionSource(
                ContextTestData.Source(),
                ContextTrust.AgentDefinition,
                0,
                ContextScope.Agent,
                ContextEvaluationFrequency.OncePerModelRequest,
                [first, second]));

        InstructionSourceProjection.ToMessages(sources).ShouldBe([first, second]);
    }
}
