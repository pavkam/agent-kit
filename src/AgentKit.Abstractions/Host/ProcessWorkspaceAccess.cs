// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the maximum workspace access a process sandbox may expose.</summary>
public enum ProcessWorkspaceAccess
{
    /// <summary>The process receives no workspace filesystem access.</summary>
    None,
    /// <summary>The process may observe workspace files but may not mutate them.</summary>
    ReadOnly,
    /// <summary>The process may observe and mutate files inside the captured workspace root.</summary>
    ReadWrite,
}
