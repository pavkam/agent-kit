// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Enumerates the bounded terminal outcomes reported for one coordinator stage.</summary>
internal enum DurableCoordinatorOutcome
{
    /// <summary>The stage completed and produced its intended durable effect.</summary>
    Completed,

    /// <summary>Recovery started work that had definitely not begun.</summary>
    Started,

    /// <summary>Recovery reconciled true external state instead of reinvoking.</summary>
    Reconciled,

    /// <summary>Recovery committed an already recorded terminal result without reinvocation.</summary>
    Committed,

    /// <summary>The stage was cancelled before completing.</summary>
    Cancelled,

    /// <summary>The stage failed for a reason other than fencing, availability, or policy.</summary>
    Failed,

    /// <summary>A required durability component or store was unavailable, so the stage failed closed.</summary>
    Unavailable,

    /// <summary>A newer worker owns the operation, so this stage may not write.</summary>
    LeaseLost,

    /// <summary>The evidence requires operator action or reconciliation before any retry.</summary>
    OperatorRequired,

    /// <summary>The persisted operation version cannot be interpreted by this build.</summary>
    Incompatible,

    /// <summary>The evidence justifies no automatic action.</summary>
    NotPossible,

    /// <summary>Security enforcement refused the durable write before any record changed.</summary>
    Denied,
}
