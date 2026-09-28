// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Verifies the loop's journaled boundary names and versions are stable configuration values.</summary>
/// <remarks>
/// These names appear in host configuration and in persisted declarations, so a rename is a breaking change to both.
/// Asserting the exact text is the cheapest way to make that visible in review.
/// </remarks>
public sealed class LoopDurableOperationsTests
{
    /// <summary>Verifies the model-attempt boundary keeps its published name.</summary>
    [Fact]
    public void ModelRequest_WhenRead_IsTheStableBoundaryName() =>
        LoopDurableOperations.ModelRequest.Value.ShouldBe("agentkit.loop.model_request");

    /// <summary>Verifies the tool-call boundary keeps its published name.</summary>
    [Fact]
    public void ToolCall_WhenRead_IsTheStableBoundaryName() =>
        LoopDurableOperations.ToolCall.Value.ShouldBe("agentkit.loop.tool_call");

    /// <summary>Verifies both state shapes declare an explicit published version.</summary>
    [Fact]
    public void Versions_WhenRead_AreExplicitlyPublished()
    {
        LoopDurableOperations.ModelRequestVersion.Value.ShouldBe("v1");
        LoopDurableOperations.ToolCallVersion.Value.ShouldBe("v1");
    }

    /// <summary>Verifies the advertised set is exactly the boundaries the loop can journal.</summary>
    [Fact]
    public void All_WhenRead_ContainsEveryJournaledBoundaryExactlyOnce() =>
        LoopDurableOperations.All.ShouldBe(
            [LoopDurableOperations.ModelRequest, LoopDurableOperations.ToolCall], ignoreOrder: true);
}
