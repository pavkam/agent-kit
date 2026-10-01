// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how one child's failure affects its siblings.</summary>
public enum DelegationFailureMode
{
    /// <summary>Every sibling runs to a terminal state and the join decides from the complete durable result set.</summary>
    SettleAllChildren = 0,

    /// <summary>The first eligible failure cancels the siblings that have not settled.</summary>
    CancelSiblings = 1,
}
