// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Controls how a requested lane operation responds when the lane is already owned.</summary>
/// <remarks>The selected behavior is captured in a session profile and applies only to acquisition policy. It does not change the one-operation-per-lane ownership invariant.</remarks>
public enum SessionBusyBehavior
{
    /// <summary>Return a busy result without waiting for the existing owner.</summary>
    Reject,

    /// <summary>Wait within the configured bound for the existing owner to release the lane.</summary>
    Wait,
}
