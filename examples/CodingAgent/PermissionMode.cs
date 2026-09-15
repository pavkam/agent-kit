// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Selects how the example handles requests to invoke tools that can change the workspace.</summary>
internal enum PermissionMode
{
    /// <summary>Requests a human decision before every write, edit, or command invocation.</summary>
    AskForChanges,

    /// <summary>Rejects every write, edit, or command invocation before it reaches the wrapped invoker.</summary>
    ReadOnly,

    /// <summary>Allows workspace write and edit invocations while continuing to ask before commands.</summary>
    AutoApproveWorkspaceEdits,
}
