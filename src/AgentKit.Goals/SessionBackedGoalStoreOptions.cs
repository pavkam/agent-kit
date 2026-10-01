// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Configures the session-backed goal-store projection.</summary>
public sealed class SessionBackedGoalStoreOptions
{
    /// <summary>Gets or sets how many times a write is replanned after another writer appended to the session first.</summary>
    /// <value>A positive attempt ceiling. The default is four.</value>
    public int MaximumAppendAttempts { get; set; } = 4;
}
