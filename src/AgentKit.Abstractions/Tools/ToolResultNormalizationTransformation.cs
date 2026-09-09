// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records an actual terminal-result normalization transformation.</summary>
/// <remarks>These observed facts are distinct from policy-permitted projection transformations and from later projection loss.</remarks>
public enum ToolResultNormalizationTransformation
{
    /// <summary>Content was redacted.</summary>
    Redacted,
    /// <summary>Content was normalized.</summary>
    Normalized,
    /// <summary>Content was summarized.</summary>
    Summarized,
    /// <summary>Content was truncated.</summary>
    Truncated,
    /// <summary>Content was externalized.</summary>
    Externalized,
}
