// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolsetVersion behavior and contracts.</summary>
public sealed class ToolsetVersionTests: Conformance.LongIdentityConformanceTests<ToolsetVersion>
{
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

    /// <inheritdoc/>
    protected override ToolsetVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ToolsetVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
