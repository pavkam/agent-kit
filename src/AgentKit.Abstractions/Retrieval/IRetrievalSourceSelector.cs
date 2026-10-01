// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Chooses which of a memory profile's configured retrieval sources serve one query.</summary>
/// <remarks>A selector only chooses among the sources its profile captured. It never discovers sources from a service provider and never selects a source the profile did not name.</remarks>
public interface IRetrievalSourceSelector
{
    /// <summary>Selects the sources for a query.</summary>
    /// <param name="query">The query the sources will serve.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns>The selected sources, or a rejection naming no content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<RetrievalSourceSelectionResult> SelectAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}
