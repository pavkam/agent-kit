// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolAliasAssignment behavior and contracts.</summary>
public sealed class ToolAliasAssignmentTests
{
    [Fact]
    public void ToolAliasAssignment_Constructor_WhenAliasDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolAliasAssignment(default, Tool()));
        exception.ParamName.ShouldBe("alias");
    }

    [Fact]
    public void ToolAliasAssignment_Constructor_WhenToolDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolAliasAssignment(new ToolAlias("read_file"), default));
        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolAliasAssignment_Constructor_WhenValid_RetainsExactFieldsEqualityHashAndCopy()
    {
        var assignment = new ToolAliasAssignment(new ToolAlias("read_file"), Tool());
        var same = new ToolAliasAssignment(new ToolAlias("read_file"), Tool());
        var copy = assignment with
        {
        };
        assignment.Alias.ShouldBe(new ToolAlias("read_file"));
        assignment.Tool.ShouldBe(Tool());
        assignment.ShouldBe(same);
        assignment.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(assignment);
        copy.ShouldNotBeSameAs(assignment);
    }

    private static ToolIdentity Tool() => new(new ToolId("read"), new ToolVersion("1.0"));
}
