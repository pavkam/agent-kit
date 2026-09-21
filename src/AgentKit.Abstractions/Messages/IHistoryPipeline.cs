// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Repairs, validates, and projects conversation history for one model request.</summary>
/// <remarks>
/// The first-party implementation performs deterministic in-memory repair and validation. A future overload may
/// accept session execution capabilities and hook dispatch context for durable reads and lifecycle observation.
/// </remarks>
public interface IHistoryPipeline
{
    /// <summary>Prepares one bounded history view from loaded messages.</summary>
    /// <param name="request">The immutable preparation evidence.</param>
    /// <param name="cancellationToken">Cancels preparation before it returns.</param>
    /// <returns>A closed preparation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public Task<HistoryPreparationResult> PrepareAsync(
        HistoryPreparationRequest request,
        CancellationToken cancellationToken = default);
}
