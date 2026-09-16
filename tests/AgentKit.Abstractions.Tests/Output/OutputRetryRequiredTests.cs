// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputRetryRequired behavior and contracts.</summary>
public sealed class OutputRetryRequiredTests
{
    [Fact]
    public void Constructor_WhenRepairIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputRetryRequired(null!, OutputTestData.Failure())).ParamName.ShouldBe("repair");

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputRetryRequired(new OutputRepairInstruction("Fix it."), null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var repair = new OutputRepairInstruction("Fix it.");
        var failure = OutputTestData.Failure();
        var required = new OutputRetryRequired(repair, failure);
        required.Repair.ShouldBe(repair);
        required.Failure.ShouldBe(failure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputRetryRequired(new OutputRepairInstruction("Fix it."), OutputTestData.Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
