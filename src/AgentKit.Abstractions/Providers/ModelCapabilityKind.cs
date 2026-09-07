// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Names one portable behavior that a request can require and a model can
/// support, so capability gaps are reported as typed values rather than
/// prose.
/// </summary>
/// <remarks>
/// This enumeration deliberately mirrors <see cref="ModelCapabilities"/> and
/// <see cref="ModelRequirements"/>. Keeping the three aligned is what lets a
/// validator report exactly which behavior is missing instead of a generic
/// incompatibility message.
/// </remarks>
public enum ModelCapabilityKind
{
    /// <summary>A system or developer instruction role distinct from user content.</summary>
    SystemInstructions,

    /// <summary>Incremental response streaming.</summary>
    Streaming,

    /// <summary>Model-requested tool calls.</summary>
    ToolCalls,

    /// <summary>Several tool calls requested in one response.</summary>
    ParallelToolCalls,

    /// <summary>Provider-enforced structured output.</summary>
    StructuredOutput,

    /// <summary>Reasoning support.</summary>
    Reasoning,

    /// <summary>Image input.</summary>
    VisionInput,

    /// <summary>A sufficiently large input context window.</summary>
    ContextWindow,
}
