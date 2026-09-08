// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Defines bounded terminal outcomes observed at the protected task-delegation dispatch boundary.</summary>
internal enum TaskDelegationPublicationOutcome
{
    /// <summary>The channel returned a child-result record after accepting dispatch.</summary>
    Dispatched,

    /// <summary>The channel rejected dispatch before reporting a child result.</summary>
    ChannelRejected,

    /// <summary>The broker rejected the grant or its enforcement receipt before dispatch.</summary>
    GrantDenied,

    /// <summary>The captured authorization did not bind the requested delegation.</summary>
    CapturedAuthorizationMismatch,

    /// <summary>The caller cancelled its wait; this does not establish that a started child was revoked.</summary>
    Cancelled,

    /// <summary>An unexpected broker, store, or channel exception ended the invocation.</summary>
    Failed,
}
