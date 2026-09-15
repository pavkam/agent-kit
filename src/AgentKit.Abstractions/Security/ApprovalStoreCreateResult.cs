// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports whether an approval request was created or already existed identically.</summary>
public enum ApprovalStoreCreateResult
{
    /// <summary>The request was created.</summary>
    Created,
    /// <summary>An identical request already exists.</summary>
    AlreadyExists,
    /// <summary>The identity is bound to different request evidence.</summary>
    Conflict,
}
