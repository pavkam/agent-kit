// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultProjectionPolicyKey behavior and contracts.</summary>
public sealed class ToolResultProjectionPolicyKeyTests: Conformance.StringIdentityConformanceTests<ToolResultProjectionPolicyKey>
{
    [Fact]
    public void PolicyKey_WhenTextIsNull_ThrowsExactArgumentNullExceptionAndParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyKey(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void PolicyKey_WhenTextIsBlank_ThrowsExactArgumentExceptionAndParameterName(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultProjectionPolicyKey(value));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void PolicyKey_WhenTextIsValid_RetainsTextAndUsesStructuralEquality()
    {
        var first = new ToolResultProjectionPolicyKey("projection.default");
        var second = new ToolResultProjectionPolicyKey("projection.default");
        first.Value.ShouldBe("projection.default");
        first.ToString().ShouldBe("projection.default");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <inheritdoc/>
    protected override ToolResultProjectionPolicyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ToolResultProjectionPolicyKey subject) => subject.Value;
}
