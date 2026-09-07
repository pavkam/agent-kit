// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies preparation of a required operating-system process sandbox.</summary>
public enum ProcessSandboxStatus
{
    /// <summary>The provider produced a launch that enforces the requested profile.</summary>
    Ready,
    /// <summary>The current platform cannot enforce the requested profile.</summary>
    Unavailable,
    /// <summary>The resolved intent requests capabilities outside the profile.</summary>
    UnsupportedIntent,
    /// <summary>Sandbox preparation failed without producing a launch.</summary>
    Failed,
}
