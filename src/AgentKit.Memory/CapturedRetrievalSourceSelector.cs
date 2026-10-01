// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Chooses among the retrieval sources one memory profile lease captured.</summary>
/// <remarks>The selector holds exactly the sources the profile names, in the profile's declared order. It never discovers sources from the container and never returns one the profile did not name.</remarks>
/// <param name="sources">The captured sources in declared order.</param>
internal sealed class CapturedRetrievalSourceSelector(ImmutableArray<IRetrievalSource> sources): IRetrievalSourceSelector
{
    /// <inheritdoc/>
    public ValueTask<RetrievalSourceSelectionResult> SelectAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            sources.IsDefaultOrEmpty
                ? RetrievalSourceSelectionResult.Rejected("The memory profile captured no retrieval source.")
                : RetrievalSourceSelectionResult.Selected(sources));
    }
}
