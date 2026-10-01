// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs the authorize, rewrite, select, search, rerank, deduplicate, expose-authorize, and budget stages of one retrieval.</summary>
/// <remarks>
/// <para>
/// The pipeline proposes authorized candidates; context assembly decides which fit and records the final manifest. It is not
/// a context assembler. Each call asks the memory-profile runtime selector for the exact profile key and version of the query,
/// holds the lease for the whole retrieval, and disposes it, so no collaborator of another agent or profile version is mixed in.
/// Returned content is untrusted data.
/// </para>
/// <para>
/// The caller's <see cref="HookDispatchContext"/> is passed separately; maintenance callers pass <see langword="null"/>. The
/// pipeline dispatches <see cref="AgentHookPoints.BeforeRetrieval"/> (a hook may only narrow the budget) before authorization
/// and <see cref="AgentHookPoints.BeforeRetrievalExposure"/> (a hook may only drop candidates) before the final budget, using
/// only that lease. A hook that fails refuses the retrieval with a typed content-free failure.
/// </para>
/// </remarks>
public interface IRetrievalPipeline
{
    /// <summary>Retrieves authorized candidates for one query.</summary>
    /// <param name="query">The query, carrying its operation context.</param>
    /// <param name="hooks">The caller's hook context, or <see langword="null"/> to dispatch no hooks.</param>
    /// <param name="cancellationToken">Cancels the retrieval.</param>
    /// <returns>Authorized, budgeted candidates with provenance, or a typed content-free failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, HookDispatchContext? hooks, CancellationToken cancellationToken = default);
}
