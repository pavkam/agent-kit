// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why a goal store refused or could not perform an operation.</summary>
public enum GoalStoreFailureKind
{
    /// <summary>The grant was missing, expired, already consumed, or did not bind this exact operation.</summary>
    Denied = 0,

    /// <summary>The goal does not exist, or belongs to another tenant.</summary>
    NotFound = 1,

    /// <summary>The expected version or observed status did not match the stored goal.</summary>
    VersionConflict = 2,

    /// <summary>An idempotency key was reused with a different request.</summary>
    IdempotencyConflict = 3,

    /// <summary>The requested status change or attempt mutation is not allowed from the stored state.</summary>
    InvalidTransition = 4,

    /// <summary>The request addressed a goal owned by another agent or session than the authorized scope.</summary>
    ScopeMismatch = 5,

    /// <summary>A child-count ceiling would be exceeded, or a parent is not open to children.</summary>
    LimitExceeded = 6,

    /// <summary>The store cannot currently perform the operation, or does not support it.</summary>
    Unavailable = 7,
}
