// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Creates unpredictable attempt identities for the default goal registration.</summary>
internal sealed class GuidGoalAttemptIdGenerator: IIdentifierGenerator<GoalAttemptId>
{
    /// <summary>Creates a new non-default attempt identity.</summary>
    /// <returns>A fresh identity.</returns>
    public GoalAttemptId Create() => new(Guid.NewGuid());
}
