// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the event that retired a captured revocation epoch.</summary>
public enum SecurityRevocationTrigger
{
    /// <summary>An operator or administrator explicitly revoked authority.</summary>
    Explicit,
    /// <summary>The effective policy snapshot that produced the authority was retired.</summary>
    PolicySnapshotRetired,
    /// <summary>The authenticated principal that received the authority was disabled.</summary>
    PrincipalDisabled,
    /// <summary>The session bound to the authority was closed.</summary>
    SessionClosed,
    /// <summary>A host distributed-ownership generation the authority depended on was retired.</summary>
    HostGenerationRetired,
}
