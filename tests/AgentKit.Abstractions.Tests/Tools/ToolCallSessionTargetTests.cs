// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolCallSessionTarget"/> validation.</summary>
public sealed class ToolCallSessionTargetTests
{
    private static readonly BranchId _branch = new(Guid.Parse("b0000000-0000-0000-0000-00000000000b"));
    private static readonly ExecutionLaneId _lane = new(Guid.Parse("b0000000-0000-0000-0000-00000000000c"));

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var target = new ToolCallSessionTarget(_branch, _lane);

        target.BranchId.ShouldBe(_branch);
        target.ExecutionLaneId.ShouldBe(_lane);
    }

    [Fact]
    public void Constructor_WhenLaneIsAbsent_AllowsSessionWideTarget() =>
        new ToolCallSessionTarget(_branch, null).ExecutionLaneId.ShouldBeNull();

    [Fact]
    public void Constructor_WhenBranchIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallSessionTarget(default, _lane)).ParamName.ShouldBe("branchId");

    [Fact]
    public void Constructor_WhenLaneIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallSessionTarget(_branch, default(ExecutionLaneId))).ParamName.ShouldBe("executionLaneId");
}
