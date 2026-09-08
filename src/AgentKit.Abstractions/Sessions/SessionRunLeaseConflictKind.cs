// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies bounded canonical-state conflicts discovered after provisional local lane ownership.</summary>
public enum SessionRunLeaseConflictKind
{
    /// <summary>The accepted total-state revision differs from the request.</summary>
    OperationStateRevision,
    /// <summary>The retained accepted profile differs from the selected profile.</summary>
    SessionProfile,
    /// <summary>The loaded accepted operation, run, lane, address, identity, or authorization differs.</summary>
    AcceptedState,
}
