// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the terminal settlement of one bounded process operation.</summary>
public enum ProcessRunStatus
{
    /// <summary>The process exited and its exit code was observed.</summary>
    Exited,
    /// <summary>Authority was rejected before process creation.</summary>
    Denied,
    /// <summary>The executable or working directory could not be resolved safely.</summary>
    ResolutionFailed,
    /// <summary>The requested sandbox could not be enforced.</summary>
    SandboxUnavailable,
    /// <summary>A configured input or resource limit rejected the operation before creation.</summary>
    LimitExceeded,
    /// <summary>The process was created but exceeded its operation timeout.</summary>
    TimedOut,
    /// <summary>The process was created and then cancelled.</summary>
    Cancelled,
    /// <summary>The process could not be created or settled reliably.</summary>
    Failed,
}
