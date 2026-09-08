// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines whether configured audit-sink delivery gates the protected operation.</summary>
public enum SecurityAuditDelivery
{
    /// <summary>Audit delivery failure is observational and the dispatcher reports accepted intent after attempting delivery.</summary>
    BestEffort,

    /// <summary>Every configured required delivery must be accepted before the protected operation may proceed.</summary>
    Required,
}
