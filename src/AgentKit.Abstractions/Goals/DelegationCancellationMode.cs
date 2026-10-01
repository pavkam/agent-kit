// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how cancelling a parent propagates to one delegated child.</summary>
public enum DelegationCancellationMode
{
    /// <summary>Cancelling the parent cancels the child. Effects the child already performed remain and are reported.</summary>
    CancelWithParent = 0,

    /// <summary>The child is durably handed off and keeps running after the parent is cancelled.</summary>
    DetachFromParent = 1,
}
