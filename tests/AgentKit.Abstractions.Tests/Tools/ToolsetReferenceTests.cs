// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolsetReference behavior and contracts.</summary>
public sealed class ToolsetReferenceTests
{
    [Fact]
    public void ToolsetReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetReference(default, new ToolExecutionPolicyKey("policy")));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ToolsetReference_Constructor_WhenExecutionPolicyKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetReference(new ToolsetKey("toolset"), default));
        exception.ParamName.ShouldBe("executionPolicyKey");
    }

    [Fact]
    public void ToolsetReference_Constructor_WhenValid_RetainsExactFieldsAndEquality()
    {
        var reference = new ToolsetReference(new ToolsetKey("toolset"), new ToolExecutionPolicyKey("policy"));
        var same = new ToolsetReference(new ToolsetKey("toolset"), new ToolExecutionPolicyKey("policy"));
        reference.Key.ShouldBe(new ToolsetKey("toolset"));
        reference.ExecutionPolicyKey.ShouldBe(new ToolExecutionPolicyKey("policy"));
        reference.ShouldBe(same);
        reference.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolsetReference(new ToolsetKey("toolset"), new ToolExecutionPolicyKey("policy"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
