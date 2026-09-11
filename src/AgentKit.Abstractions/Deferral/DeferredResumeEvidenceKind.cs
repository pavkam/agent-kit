// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the correlated evidence required before a deferred request can advance.</summary>
public enum DeferredResumeEvidenceKind
{
    /// <summary>An authenticated approval resolution and fresh validation of the exact protected operation are required.</summary>
    ApprovalResolution,
    /// <summary>An authenticated correlated terminal result with effect certainty and idempotency evidence is required.</summary>
    OperationResult,
    /// <summary>The original provider/account/model/API/request-bound continuation handle and authorized poll or drive are required.</summary>
    ProviderContinuation,
}
