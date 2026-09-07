// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The operation definitely never started, so it may be begun for the first
/// time.
/// </summary>
/// <remarks>
/// This decision requires proof, not merely an absent record. It is valid
/// when <see cref="RecoveryEvidence.StartDefinitelyAbsent"/> is
/// <see langword="true"/> or the effect is otherwise known not to have been
/// dispatched. Choosing it on weaker evidence is how recovery duplicates
/// external effects.
/// </remarks>
public sealed record RecoveryStartOperation: RecoveryDecision
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryStartOperation"/> record.
    /// </summary>
    public RecoveryStartOperation()
    {
    }
}
