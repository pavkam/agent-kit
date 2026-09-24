// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request was cancelled before a terminal protocol outcome was observed.</summary>
/// <param name="RequestId">The completed MCP request identity.</param>
/// <param name="SideEffectCertainty">Evidence about whether an effect may have started.</param>
public sealed record McpResponseCancelled(
    McpRequestId RequestId,
    SideEffectCertainty SideEffectCertainty): McpResponse(RequestId);
