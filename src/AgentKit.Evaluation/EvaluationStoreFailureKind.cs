// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies why a result store refused an operation.</summary>
public enum EvaluationStoreFailureKind
{
    /// <summary>The result conflicts with a different result already recorded for the same identity, or pins a run to a second plan.</summary>
    IdentityConflict = 0,

    /// <summary>The encoded result exceeds the bound the adapter enforces.</summary>
    LimitExceeded = 1,

    /// <summary>The store cannot currently serve the operation, for example because its target is unreachable.</summary>
    Unavailable = 2,
}
