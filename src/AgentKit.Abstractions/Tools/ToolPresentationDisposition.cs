// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States whether a tool presentation is feature-authored, bounded, or generic fallback output.</summary>
public enum ToolPresentationDisposition
{
    /// <summary>The selected formatter produced complete output.</summary>
    Formatted,
    /// <summary>Output was shortened to its explicit bound.</summary>
    Truncated,
    /// <summary>No exact formatter could safely format the source, so generic literal output was used.</summary>
    Fallback,
}
