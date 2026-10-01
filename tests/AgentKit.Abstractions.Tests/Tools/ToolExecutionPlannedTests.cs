// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPlanned"/>.</summary>
public sealed class ToolExecutionPlannedTests
{
    [Fact]
    public void Constructor_WhenCallsAreValid_RoundTripsOrderedCalls()
    {
        var calls = ImmutableArray.Create(PreparedCall(), PreparedCall(sourceOrdinal: 1));
        var planned = new ToolExecutionPlanned(calls);

        planned.Calls.ShouldBe(calls);
        planned.Equals(new ToolExecutionPlanned(calls)).ShouldBeTrue();
        planned.GetHashCode().ShouldBe(new ToolExecutionPlanned(calls).GetHashCode());
        planned.Equals(new ToolExecutionPlanned([calls[1], calls[0]])).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenCallsAreUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolExecutionPlanned(default)).ParamName.ShouldBe("calls");

    [Fact]
    public void Constructor_WhenCallsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolExecutionPlanned([null!])).ParamName.ShouldBe("calls");
}
