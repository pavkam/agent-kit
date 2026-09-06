// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The lifecycle state of one session, recorded on its
/// <see cref="SessionDescriptor"/>.
/// </summary>
public enum SessionLifecycleState
{
    /// <summary>The session accepts new appends and runs.</summary>
    Active,

    /// <summary>
    /// The session is temporarily not accepting new mutating runs, but its
    /// record remains readable.
    /// </summary>
    Suspended,

    /// <summary>
    /// The session no longer accepts new appends or runs, but its record
    /// remains readable and auditable.
    /// </summary>
    Closed,

    /// <summary>
    /// The session has been moved to long-term, read-mostly storage by a
    /// retention policy.
    /// </summary>
    Archived
}
