// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies canonical process-intent resolution before authorization.</summary>
public enum ProcessResolutionStatus
{
    /// <summary>The executable and working directory were resolved and fingerprinted.</summary>
    Resolved,
    /// <summary>The executable reference is unavailable or outside configured policy.</summary>
    ExecutableRejected,
    /// <summary>The working directory is unavailable or outside the captured workspace.</summary>
    WorkingDirectoryRejected,
    /// <summary>The environment, input, or requested bounds violate the host profile.</summary>
    InvalidIntent,
    /// <summary>Secure resolution is unsupported or failed without observing a usable intent.</summary>
    Failed,
}
