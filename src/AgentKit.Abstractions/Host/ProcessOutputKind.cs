// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one process byte stream without conflating stdout and stderr.</summary>
public enum ProcessOutputKind
{
    /// <summary>The process standard-output stream.</summary>
    StandardOutput,
    /// <summary>The process standard-error stream.</summary>
    StandardError,
}
