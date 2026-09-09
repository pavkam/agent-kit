// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolsetPrimitiveTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ToolsetKey_Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolsetKey(value));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolsetKey_Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolsetKey(null!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolsetKey_Constructor_WhenValid_PreservesOrdinalTextAndDefaultFormatting()
    {
        var key = new ToolsetKey("A");

        key.Value.ShouldBe("A");
        key.ShouldNotBe(new ToolsetKey("a"));
        default(ToolsetKey).ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToolsetVersion_Constructor_WhenNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolsetVersion_Constructor_WhenPositive_PreservesMaximumValueAndFormatsInvariantly()
    {
        var version = new ToolsetVersion(long.MaxValue);

        version.Value.ShouldBe(long.MaxValue);
        version.ToString().ShouldBe("9223372036854775807");
    }

    [Fact]
    public void ToolsetReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolsetReference(default, new ToolExecutionPolicyKey("policy")));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ToolsetReference_Constructor_WhenExecutionPolicyKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolsetReference(new ToolsetKey("toolset"), default));

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

    [Fact]
    public void ToolAliasAssignment_Constructor_WhenAliasDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolAliasAssignment(default, Tool()));

        exception.ParamName.ShouldBe("alias");
    }

    [Fact]
    public void ToolAliasAssignment_Constructor_WhenToolDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolAliasAssignment(new ToolAlias("read_file"), default));

        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolAliasAssignment_Constructor_WhenValid_RetainsExactFieldsEqualityHashAndCopy()
    {
        var assignment = new ToolAliasAssignment(new ToolAlias("read_file"), Tool());
        var same = new ToolAliasAssignment(new ToolAlias("read_file"), Tool());
        var copy = assignment with { };

        assignment.Alias.ShouldBe(new ToolAlias("read_file"));
        assignment.Tool.ShouldBe(Tool());
        assignment.ShouldBe(same);
        assignment.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(assignment);
        copy.ShouldNotBeSameAs(assignment);
    }

    private static ToolIdentity Tool() => new(new ToolId("read"), new ToolVersion("1.0"));
}
