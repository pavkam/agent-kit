// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Distinguishes why an operation requires a later correlated resolution.</summary>
public enum DeferralKind
{
    /// <summary>Unstarted work awaits authenticated approval followed by fresh authorization.</summary>
    ApprovalRequired,
    /// <summary>Unstarted work has been handed to an external workflow for execution and result delivery.</summary>
    OperationDeferred,
    /// <summary>Execution has started and a correlated result remains pending.</summary>
    ResultPending,
    /// <summary>A provider continuation remains owned by the open runtime operation and requires an explicit poll or drive.</summary>
    ProviderSuspended,
}
