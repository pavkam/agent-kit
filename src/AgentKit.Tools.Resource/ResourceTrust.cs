// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Records source provenance without converting loaded text into authority.</summary>
public enum ResourceTrust
{
    /// <summary>Managed host-owned content.</summary>
    Managed,
    /// <summary>User-owned content outside the repository.</summary>
    User,
    /// <summary>Installed package content.</summary>
    Package,
    /// <summary>Repository or workspace-controlled content.</summary>
    Workspace,
    /// <summary>Invocation-specific content.</summary>
    Invocation,
}
