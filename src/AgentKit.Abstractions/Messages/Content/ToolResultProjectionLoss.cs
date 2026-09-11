// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one loss recorded while projecting an authoritative tool result into history or model content.</summary>
/// <remarks>These are projection evidence, not permission to transform content. The captured projection policy determines which transformations are allowed.</remarks>
public enum ToolResultProjectionLoss
{
    /// <summary>Content was removed or replaced to satisfy the captured redaction rules.</summary>
    Redacted,

    /// <summary>Content was converted into a portable representation that does not retain its original form.</summary>
    Normalized,

    /// <summary>Content was replaced by a summary that omits source detail.</summary>
    Summarized,

    /// <summary>Content was shortened or parts were removed to satisfy projection bounds.</summary>
    Truncated,

    /// <summary>Content was replaced by an authorized artifact or continuation reference.</summary>
    Externalized,

    /// <summary>The portable outcome cannot express the exact source terminal status retained alongside it.</summary>
    StatusCoarsened,
}
