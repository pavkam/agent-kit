// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>
/// Controls what happens when a run requests the active-run lease for a
/// session that already has one.
/// </summary>
public enum SessionBusyBehavior
{
    /// <summary>
    /// Immediately return <see cref="SessionRunBusy"/> without waiting.
    /// </summary>
    Reject,

    /// <summary>
    /// Wait up to <see cref="AgentSessionOptions.BusyWaitTimeout"/> for the
    /// active run to release the lease before returning
    /// <see cref="SessionRunBusy"/>.
    /// </summary>
    Wait
}
