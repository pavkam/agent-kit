// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies captured observation payload semantics for policy and redaction.</summary>
/// <remarks>Each kind may be enabled, bounded, and redacted independently; enabling one kind never opts into another.</remarks>
public enum ObservationContentKind
{
    /// <summary>Model or developer instruction text.</summary>
    Prompt,

    /// <summary>Model-generated assistant output.</summary>
    ModelOutput,

    /// <summary>Tool invocation arguments.</summary>
    ToolArguments,

    /// <summary>Tool execution results.</summary>
    ToolResult,

    /// <summary>Retrieved or injected context documents.</summary>
    RetrievedContent,

    /// <summary>Provider reasoning or thinking metadata.</summary>
    Reasoning,
}
