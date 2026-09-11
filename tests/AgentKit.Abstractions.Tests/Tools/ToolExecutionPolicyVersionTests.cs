// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;



/// <summary>Verifies ToolExecutionPolicyVersion behavior and contracts.</summary>
public sealed class ToolExecutionPolicyVersionTests: Conformance.LongIdentityConformanceTests<ToolExecutionPolicyVersion>
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public void ToolExecutionPolicyVersion_Constructor_WhenValueNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyVersion(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolExecutionPolicyVersion_Constructor_WhenValuePositive_PreservesValue()
    {
        var version = new ToolExecutionPolicyVersion(1);
        version.Value.ShouldBe(1);
        version.ToString().ShouldBe("1");
    }

    [Fact]
    public void ToolExecutionPolicyVersion_Constructor_WhenValueMaximum_PreservesValue()
    {
        var version = new ToolExecutionPolicyVersion(long.MaxValue);
        version.Value.ShouldBe(long.MaxValue);
        version.ToString().ShouldBe("9223372036854775807");
    }

    /// <inheritdoc/>
    protected override ToolExecutionPolicyVersion Create(long value) => new(value);
    /// <inheritdoc/>
    protected override long GetValue(ToolExecutionPolicyVersion subject) => subject.Value;
    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolExecutionPolicyVersion).ShouldBe(default);
        default(ToolExecutionPolicyVersion).ToString().ShouldBe("0");
    }
    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
