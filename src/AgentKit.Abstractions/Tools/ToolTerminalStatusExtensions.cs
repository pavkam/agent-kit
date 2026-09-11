// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the provider-neutral, closed projection from exact terminal status to portable outcome.</summary>
/// <remarks>The mapping never inspects error text, effect certainty, or provider-specific content.</remarks>
public static class ToolTerminalStatusExtensions
{
    /// <summary>Maps retained terminal status values without rejecting unknown future values.</summary>
    /// <param name="status">The exact persisted status, including any unrecognized numeric value.</param>
    extension(ToolTerminalStatus status)
    {
        /// <summary>Returns the portable disposition corresponding to the terminal stage.</summary>
        /// <returns>Success only for a successful terminal status; rejection for pre-invocation rejections, cancellation for cancellation/interruption, and failure otherwise.</returns>
        /// <remarks>Unknown values map to failure. The caller retains the original status and records <see cref="ToolResultProjectionLoss.StatusCoarsened"/> when producing its projection.</remarks>
        public ToolCallOutcomeKind ToOutcomeKind() => status switch
        {
            ToolTerminalStatus.Succeeded => ToolCallOutcomeKind.Success,
            ToolTerminalStatus.UnknownTool or ToolTerminalStatus.InvalidArguments or ToolTerminalStatus.Unsupported
                or ToolTerminalStatus.Denied or ToolTerminalStatus.ApprovalDenied or ToolTerminalStatus.ApprovalExpired
                => ToolCallOutcomeKind.Rejected,
            ToolTerminalStatus.Cancelled or ToolTerminalStatus.Interrupted => ToolCallOutcomeKind.Cancelled,
            ToolTerminalStatus.InvocationFailed or ToolTerminalStatus.TimedOut
                or ToolTerminalStatus.ResultNormalizationFailed or ToolTerminalStatus.ResultSerializationFailed
                or ToolTerminalStatus.ProtocolFailed => ToolCallOutcomeKind.Failed,
            _ => ToolCallOutcomeKind.Failed,
        };
    }
}
