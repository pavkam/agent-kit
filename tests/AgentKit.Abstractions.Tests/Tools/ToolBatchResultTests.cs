// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolBatchResult behavior and contracts.</summary>
public sealed class ToolBatchResultTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<ToolCallResult> results = [RejectedCallResult()];
        var batchResult = new ToolBatchResult(results);
        batchResult.Results.ShouldBe(results);
    }

    [Fact]
    public void Constructor_WhenResultsIsEmpty_Succeeds()
    {
        var batchResult = new ToolBatchResult([]);
        batchResult.Results.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenResultsIsUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatchResult(default)).ParamName.ShouldBe("results");

    [Fact]
    public void Constructor_WhenResultsContainsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatchResult([null!])).ParamName.ShouldBe("results");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolBatchResult([RejectedCallResult()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
