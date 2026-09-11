// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolSchemaCompilationRejectedTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Constructor_WhenReasonUndefined_RejectsExactArgument(int reason) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolSchemaCompilationRejected((ToolSchemaRejectionReason) reason)).ParamName.ShouldBe("reason");
    [Theory]
    [InlineData(ToolSchemaRejectionReason.UnsupportedDialect)]
    [InlineData(ToolSchemaRejectionReason.UnsupportedKeyword)]
    [InlineData(ToolSchemaRejectionReason.InvalidSchema)]
    [InlineData(ToolSchemaRejectionReason.ResourceLimitExceeded)]
    public void Constructor_WhenReasonDefined_RetainsTypedRejection(ToolSchemaRejectionReason reason) =>
        new ToolSchemaCompilationRejected(reason).Reason.ShouldBe(reason);
}
