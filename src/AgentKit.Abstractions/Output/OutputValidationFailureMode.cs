// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares whether validation stops at the first failing validator or runs every validator.</summary>
public enum OutputValidationFailureMode
{
    /// <summary>Stop at the first validator that reports an issue.</summary>
    RejectOnFirstFailure,

    /// <summary>Run every registered validator and aggregate every reported issue.</summary>
    CollectAllFailures
}
