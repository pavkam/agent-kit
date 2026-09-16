// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmToolDefinition behavior and contracts.</summary>
public sealed class LlmToolDefinitionTests
{
    [Fact]
    public void LlmToolDefinition_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolDefinition(new ToolId("t"), " ", null, default));
    [Fact]
    public void LlmToolDefinition_Equality_WhenSameValues_InstancesAreEqual() => new LlmToolDefinition(new ToolId("t"), "tool", null, default).ShouldBe(new LlmToolDefinition(new ToolId("t"), "tool", null, default));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LlmToolDefinition(new ToolId("t"), "tool", null, default);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
