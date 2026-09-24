// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Transport open was cancelled before completion.</summary>
/// <param name="SideEffectCertainty">Evidence about whether transport effects may have started.</param>
public sealed record McpTransportOpenCancelled(SideEffectCertainty SideEffectCertainty): McpTransportOpenResult;
