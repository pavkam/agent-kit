// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Classifies the outcome of reducing one request over stored goal state.</summary>
internal enum GoalReductionKind
{
    /// <summary>The request was valid and produced a new aggregate that the store must persist.</summary>
    Applied = 0,

    /// <summary>An equivalent earlier request already produced the stored state; nothing is persisted.</summary>
    Replayed = 1,

    /// <summary>The request was refused and stored state is unchanged.</summary>
    Rejected = 2,
}
