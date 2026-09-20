// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

/// <summary>Closes run-plan compilation to a compiled plan or a diagnostic rejection.</summary>
/// <remarks>
/// Compilation failure is configuration evidence. It is not a run identity and it authorizes no session mutation.
/// </remarks>
internal abstract record AgentRunPlanCompilationResult
{
    /// <summary>Prevents external variants. This family is closed to the two records in this assembly.</summary>
    private protected AgentRunPlanCompilationResult()
    {
    }
}
