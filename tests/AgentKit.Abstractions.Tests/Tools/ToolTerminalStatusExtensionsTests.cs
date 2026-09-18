// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies the public terminal-status projection mapping, including forward-compatible unknown values.</summary>
public sealed class ToolTerminalStatusExtensionsTests
{
    [Theory]
    [InlineData(ToolTerminalStatus.Succeeded, ToolCallOutcomeKind.Success)]
    [InlineData(ToolTerminalStatus.UnknownTool, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.InvalidArguments, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.Unsupported, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.Denied, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.ApprovalDenied, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.ApprovalExpired, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.TimedOut, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.Cancelled, ToolCallOutcomeKind.Cancelled)]
    [InlineData(ToolTerminalStatus.Interrupted, ToolCallOutcomeKind.Cancelled)]
    [InlineData(ToolTerminalStatus.ResultNormalizationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ResultSerializationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ProtocolFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ResourceLimitExceeded, ToolCallOutcomeKind.Rejected)]
    public void ToOutcomeKind_WhenStatusIsKnown_MapsItsTerminalStage(ToolTerminalStatus status, ToolCallOutcomeKind expected) =>
        status.ToOutcomeKind().ShouldBe(expected);

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(int.MaxValue)]
    public void ToOutcomeKind_WhenStatusIsUnknown_MapsToFailure(int rawStatus) =>
        ((ToolTerminalStatus) rawStatus).ToOutcomeKind().ShouldBe(ToolCallOutcomeKind.Failed);
}
