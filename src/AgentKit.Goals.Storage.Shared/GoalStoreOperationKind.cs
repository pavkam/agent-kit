// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Names the bounded goal-store operations used as a trace and metric dimension.</summary>
internal enum GoalStoreOperationKind
{
    /// <summary>A goal creation.</summary>
    Create = 0,

    /// <summary>A goal load.</summary>
    Load = 1,

    /// <summary>A goal transition.</summary>
    Transition = 2,

    /// <summary>A children page read.</summary>
    ReadChildren = 3,

    /// <summary>An intent discovery page read.</summary>
    ReadIntents = 4,
}
