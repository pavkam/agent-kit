// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a failed durable-abort precondition without parsing text.</summary>
public enum SessionRunAbortRejectionKind
{
    /// <summary>The selected store or coordinator does not support durable run abort.</summary>
    Unsupported,

    /// <summary>The session or lane does not exist, or the caller's tenant does not own it.</summary>
    LaneNotFound,

    /// <summary>The lane exists but currently holds no accepted run to abort.</summary>
    NoAcceptedRun,

    /// <summary>The lane's installed accepted run does not match the caller's operation, run, or expected revision.</summary>
    Fenced,

    /// <summary>The canonical whole-session version changed since the caller last observed it.</summary>
    SessionVersion,

    /// <summary>The idempotency identity was reused with different evidence.</summary>
    Idempotency,
}
