// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Verifies AgentOutputOptionsSnapshot behavior and contracts.</summary>
public sealed class AgentOutputOptionsSnapshotTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactValues()
    {
        var snapshot = new AgentOutputOptionsSnapshot(1, 2, 3, 4, 5, 6, 7, 8, true, false);

        snapshot.MaximumCandidateBytes.ShouldBe(1);
        snapshot.MaximumSchemaBytes.ShouldBe(2);
        snapshot.MaximumSchemaDepth.ShouldBe(3);
        snapshot.MaximumSchemaNodes.ShouldBe(4);
        snapshot.MaximumCandidateDepth.ShouldBe(5);
        snapshot.MaximumCandidateNodes.ShouldBe(6);
        snapshot.MaximumValidationIssues.ShouldBe(7);
        snapshot.MaximumRepairAttempts.ShouldBe(8);
        snapshot.RequireSchemaForStructuredModes.ShouldBeTrue();
        snapshot.AllowProviderModeDowngrade.ShouldBeFalse();
    }

    [Fact]
    public void With_WhenNoMembersChanged_ProducesAnEqualClone()
    {
        var snapshot = new AgentOutputOptionsSnapshot(1, 2, 3, 4, 5, 6, 7, 8, true, false);

        var clone = snapshot with { };

        clone.ShouldBe(snapshot);
        clone.ShouldNotBeSameAs(snapshot);
    }

    [Theory]
    [InlineData(0, "maximumCandidateBytes")]
    [InlineData(-1, "maximumCandidateBytes")]
    public void Constructor_WhenMaximumCandidateBytesIsNotPositive_ThrowsExactParameterName(int value, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new AgentOutputOptionsSnapshot(value, 1, 1, 1, 1, 1, 1, 0, true, true));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenMaximumRepairAttemptsIsNegative_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new AgentOutputOptionsSnapshot(1, 1, 1, 1, 1, 1, 1, -1, true, true));

        exception.ParamName.ShouldBe("maximumRepairAttempts");
    }

    [Theory]
    [InlineData(129, true)]
    [InlineData(129, false)]
    public void Constructor_WhenADepthExceeds128_ThrowsExactParameterName(int depth, bool schemaDepth)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentOutputOptionsSnapshot(
            1, 1, schemaDepth ? depth : 1, 1, schemaDepth ? 1 : depth, 1, 1, 0, true, true));

        exception.ParamName.ShouldBe(schemaDepth ? "maximumSchemaDepth" : "maximumCandidateDepth");
    }
}
