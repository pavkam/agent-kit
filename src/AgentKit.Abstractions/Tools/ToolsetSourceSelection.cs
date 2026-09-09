// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;

/// <summary>Selects one tool-source identity without resolving publication membership.</summary>
/// <remarks>Run catalog capture pins the source version returned by discovery; this authored selection performs no lookup or authority grant.</remarks>
public sealed record ToolsetSourceSelection
{
    /// <summary>Creates an authored source selection without predicting a dynamic source version.</summary>
    /// <param name="sourceId">The nondefault stable source identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
    public ToolsetSourceSelection(ToolSourceId sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        SourceId = sourceId;
    }
    /// <summary>Gets the selected discovery source.</summary>
    /// <value>A nondefault identity whose returned version is pinned during catalog capture.</value>
    public ToolSourceId SourceId { get; }
}
