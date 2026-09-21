// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects context candidates within a bounded token envelope.</summary>
/// <remarks>
/// Implementations reserve mandatory content first, then optional content in descending priority order. They do not
/// mutate candidates or invoke providers.
/// </remarks>
public interface IContextBudgetAllocator
{
    /// <summary>Allocates one bounded candidate set for assembly.</summary>
    /// <param name="request">The budget envelope and candidate evidence.</param>
    /// <param name="cancellationToken">Cancels allocation before it returns.</param>
    /// <returns>A plan describing selected and omitted candidates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<ContextBudgetPlan> AllocateAsync(
        ContextBudgetRequest request,
        CancellationToken cancellationToken = default);
}
