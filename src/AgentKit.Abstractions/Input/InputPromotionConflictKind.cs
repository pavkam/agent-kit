// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies stale evidence that prevents atomic promotion.</summary>
public enum InputPromotionConflictKind
{
    /// <summary>The lane no longer owns the expected operation.</summary>
    OperationChanged,
    /// <summary>The lane operation-state revision changed.</summary>
    OperationRevisionChanged,
    /// <summary>The branch tip changed.</summary>
    BranchCursorChanged,
    /// <summary>The optional session CAS failed.</summary>
    SessionVersionChanged,
    /// <summary>The distributed owner supplied no fence or a stale fence.</summary>
    Fenced,
    /// <summary>The exact planned admissions are no longer eligible.</summary>
    EligibleInputChanged,
}
