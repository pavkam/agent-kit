// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolExecutionPlanRejected"/>.</summary>
public sealed class ToolExecutionPlanRejectedTests
{
    [Fact]
    public void Constructor_WhenReasonIsValid_RoundTripsReason() =>
        new ToolExecutionPlanRejected("No capacity.").SafeReason.ShouldBe("No capacity.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenReasonIsBlank_ThrowsExactParameter(string? reason) =>
        Should.Throw<ArgumentException>(() => new ToolExecutionPlanRejected(reason!)).ParamName.ShouldBe("safeReason");
}
