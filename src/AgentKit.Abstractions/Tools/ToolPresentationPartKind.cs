// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies one literal, provider-neutral tool presentation part for application rendering.</summary>
public enum ToolPresentationPartKind
{
    /// <summary>Plain explanatory text.</summary>
    Text,
    /// <summary>Literal source, command, or output text suitable for fixed-width rendering.</summary>
    Code,
    /// <summary>A literal textual difference suitable for a diff renderer.</summary>
    Diff,
}
