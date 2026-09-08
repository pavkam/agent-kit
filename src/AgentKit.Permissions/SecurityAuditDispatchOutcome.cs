// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Classifies the bounded terminal result of one security-audit delivery attempt.</summary>
internal enum SecurityAuditDispatchOutcome
{
    /// <summary>At least the required durable acceptance policy was satisfied.</summary>
    Accepted,

    /// <summary>No compatible durable sink could satisfy the required policy.</summary>
    Unavailable,

    /// <summary>A required sink failed before the delivery policy was satisfied.</summary>
    Failed,

    /// <summary>The caller cancelled delivery before the dispatcher reached a terminal result.</summary>
    Cancelled,
}
