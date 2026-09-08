// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that committed terminal tool records require model interpretation.</summary>
public sealed record CommittedToolResultsContinuationCause: RunContinuationCause
{
    /// <summary>Initializes committed-tool evidence.</summary>
    /// <param name="toolResults">The nonempty unique terminal-record references.</param>
    /// <exception cref="ArgumentException"><paramref name="toolResults"/> is default, empty, contains null, or duplicates correlation.</exception>
    public CommittedToolResultsContinuationCause(ImmutableArray<CommittedToolResultReference> toolResults)
    {
        ArgumentException.ThrowIfContainsNull(toolResults);
        ArgumentException.ThrowIfDefaultOrEmpty(toolResults);
        ArgumentException.ThrowIfDuplicateCommittedToolReferences(toolResults);
        ToolResults = toolResults;
    }

    /// <summary>Gets terminal-record references whose commit is established by the session owner.</summary>
    public ImmutableArray<CommittedToolResultReference> ToolResults { get; }
}
