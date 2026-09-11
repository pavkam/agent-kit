// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultProjectionPolicyVersion behavior and contracts.</summary>
public sealed class ToolResultProjectionPolicyVersionTests: Conformance.LongIdentityConformanceTests<ToolResultProjectionPolicyVersion>
{
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void PolicyVersion_WhenValueIsNotPositive_ThrowsExactParameterName(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicyVersion(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void PolicyVersion_WhenValueIsPositive_RetainsInvariantText()
    {
        var version = new ToolResultProjectionPolicyVersion(7);
        version.Value.ShouldBe(7);
        version.ToString().ShouldBe("7");
    }

    [Fact]
    public void PolicyVersion_WhenValueIsMaximum_RetainsValue()
    {
        var version = new ToolResultProjectionPolicyVersion(long.MaxValue);
        version.Value.ShouldBe(long.MaxValue);
    }

    /// <inheritdoc/>
    protected override ToolResultProjectionPolicyVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ToolResultProjectionPolicyVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
