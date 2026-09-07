// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares whether the sandbox may permit the admitted executable to create further processes.</summary>
public enum ProcessChildPolicy
{
    /// <summary>Child creation and execution must be denied by the selected sandbox.</summary>
    Deny,
    /// <summary>Children may run only inside the same inherited sandbox and projected authorities.</summary>
    AllowSandboxed,
}
