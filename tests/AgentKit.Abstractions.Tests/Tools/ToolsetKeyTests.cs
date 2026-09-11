// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolsetKey behavior and contracts.</summary>
public sealed class ToolsetKeyTests: Conformance.StringIdentityConformanceTests<ToolsetKey>
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

    /// <inheritdoc/>
    protected override ToolsetKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ToolsetKey subject) => subject.Value;
}
