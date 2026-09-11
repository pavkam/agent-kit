// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States whether execution started before deferral without claiming effect completion.</summary>
public enum DeferralEffectState
{
    /// <summary>Affirmative evidence establishes that the deferred execution has not started.</summary>
    NotStarted,
    /// <summary>Execution started; its effect and result may remain unknown and must not be replayed speculatively.</summary>
    Started,
}
