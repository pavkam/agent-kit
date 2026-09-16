// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputRepairInstruction behavior and contracts.</summary>
public sealed class OutputRepairInstructionTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputRepairInstruction(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSafeMessage()
    {
        var instruction = new OutputRepairInstruction("Fix the JSON.");
        instruction.SafeMessage.ShouldBe("Fix the JSON.");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputRepairInstruction("Fix the JSON.");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
