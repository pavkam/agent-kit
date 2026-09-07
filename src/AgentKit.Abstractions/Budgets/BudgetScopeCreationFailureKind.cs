// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of a failure encountered while creating a budget scope.</summary>
public enum BudgetScopeCreationFailureKind
{
    /// <summary>The requested parent scope does not exist.</summary>
    ParentNotFound,

    /// <summary>Creating this scope would exceed the configured maximum hierarchy depth.</summary>
    MaximumDepthExceeded,

    /// <summary>
    /// A limit configured on the new scope is wider than a hard limit for
    /// the same dimension configured on an ancestor scope.
    /// </summary>
    LimitWiderThanAncestor,

    /// <summary>A limit references a dimension with no registered descriptor, or an unsupported unit.</summary>
    InvalidLimit
}
