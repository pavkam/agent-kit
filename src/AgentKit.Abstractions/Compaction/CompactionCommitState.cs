// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// What is known about whether a compaction attempt's activation append
/// reached the session when the attempt ended early.
/// </summary>
/// <remarks>
/// Cancellation before activation discards only an unpublished candidate;
/// cancellation during or after activation may have left a committed
/// <see cref="CompactionSessionEntry"/> behind. The compactor reconciles the
/// branch by <see cref="CompactionId"/> before reporting, so a caller can
/// trust <see cref="Committed"/> and <see cref="NotCommitted"/> and must
/// resolve <see cref="Unknown"/> before any second activation attempt.
/// </remarks>
public enum CompactionCommitState
{
    /// <summary>The attempt ended before any activation append was issued; nothing durable changed.</summary>
    NotAttempted,

    /// <summary>An activation append was issued and reconciliation proved no record was committed.</summary>
    NotCommitted,

    /// <summary>An activation append was issued and reconciliation found the committed record.</summary>
    Committed,

    /// <summary>An activation append was issued but reconciliation could not establish whether it committed.</summary>
    Unknown
}
