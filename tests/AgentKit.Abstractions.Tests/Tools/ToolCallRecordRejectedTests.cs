// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolCallRecordRejected"/>.</summary>
public sealed class ToolCallRecordRejectedTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejected = new ToolCallRecordRejected(ToolCallRecordRejectionKind.Conflict, "Advanced.");

        rejected.Kind.ShouldBe(ToolCallRecordRejectionKind.Conflict);
        rejected.SafeReason.ShouldBe("Advanced.");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallRecordRejected((ToolCallRecordRejectionKind) 99, "x")).ParamName.ShouldBe("kind");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenReasonIsBlank_ThrowsExactParameter(string? reason) =>
        Should.Throw<ArgumentException>(() => new ToolCallRecordRejected(ToolCallRecordRejectionKind.Unavailable, reason!)).ParamName.ShouldBe("safeReason");
}
