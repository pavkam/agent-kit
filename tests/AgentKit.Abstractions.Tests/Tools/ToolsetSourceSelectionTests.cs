// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolsetSourceSelection behavior and contracts.</summary>
public sealed class ToolsetSourceSelectionTests
{
    [Fact]
    public void ToolsetSourceSelection_Constructor_WhenSourceDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetSourceSelection(default));
        exception.ParamName.ShouldBe("sourceId");
    }

    [Fact]
    public void ToolsetSourceSelection_Constructor_WhenValid_RetainsExactSource()
    {
        var selection = new ToolsetSourceSelection(new ToolSourceId("agentkit.tools.files"));
        selection.SourceId.ShouldBe(new ToolSourceId("agentkit.tools.files"));
    }
}
