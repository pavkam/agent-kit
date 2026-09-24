// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request ended without a definitive protocol outcome after an effect may have started.</summary>
public sealed record McpResponseUnknownEffect: McpResponse
{
    /// <summary>Initializes an unknown-effect response.</summary>
    /// <param name="requestId">The completed MCP request identity.</param>
    /// <param name="safeMessage">A non-sensitive summary suitable for logs and callers.</param>
    /// <param name="sideEffectCertainty">Evidence about whether the effect completed.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpResponseUnknownEffect(
        McpRequestId requestId,
        string safeMessage,
        SideEffectCertainty sideEffectCertainty)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the non-sensitive summary.</summary>
    public string SafeMessage { get; }

    /// <summary>Gets evidence about whether the effect completed.</summary>
    public SideEffectCertainty SideEffectCertainty { get; }
}
