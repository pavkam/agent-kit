// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the bounded action a recovery policy chose, for observation and metric dimensions.</summary>
/// <remarks>
/// Each member corresponds to exactly one concrete <see cref="RecoveryDecision"/> kind. The enum exists because
/// metrics require bounded dimensions and a decision record may carry a payload, an external handle, or a safe
/// message that must not become a tag value.
/// </remarks>
public enum DurableRecoveryAction
{
    /// <summary>The operation was proven unstarted and may begin. See <see cref="RecoveryStartOperation"/>.</summary>
    Start,

    /// <summary>An external owner must be queried for the true outcome. See <see cref="RecoveryReconcileOperation"/>.</summary>
    Reconcile,

    /// <summary>The effect is safe to invoke again. See <see cref="RecoveryRetryOperation"/>.</summary>
    Retry,

    /// <summary>A recorded terminal result must be committed without reinvocation. See <see cref="RecoveryCommitRecordedResult"/>.</summary>
    CommitRecordedResult,

    /// <summary>No automatic action is safe and a human must decide. See <see cref="RecoveryRequiresOperator"/>.</summary>
    RequireOperator,

    /// <summary>The evidence justifies no action at all. See <see cref="RecoveryNotPossible"/>.</summary>
    NotPossible,
}
