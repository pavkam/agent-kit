// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one <see cref="IModelRequestExecutor"/> call.</summary>
/// <remarks>
/// Success, same-model exhaustion that should fall back, terminal failure, and
/// cancellation are distinct. A fallback result is not a failure of the turn:
/// the loop may select another candidate. Cancellation is never retried.
/// </remarks>
public abstract record ModelExecutionResult
{
    private protected ModelExecutionResult()
    {
    }
}
