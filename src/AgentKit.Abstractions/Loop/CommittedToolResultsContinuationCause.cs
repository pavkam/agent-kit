// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies evidence that committed terminal tool records require another model request for interpretation.</summary>
/// <remarks>The references preserve correlation only; the session owner remains responsible for proving the records committed and for materializing any history projection.</remarks>
public sealed record CommittedToolResultsContinuationCause: RunContinuationCause
{
    /// <summary>Initializes the committed tool-result evidence for a continuation proposal.</summary>
    /// <param name="toolResults">A non-default, nonempty immutable collection of uniquely correlated terminal-record references.</param>
    /// <exception cref="ArgumentException"><paramref name="toolResults"/> is default, empty, contains null, or duplicates correlation.</exception>
    public CommittedToolResultsContinuationCause(ImmutableArray<CommittedToolResultReference> toolResults)
    {
        ArgumentException.ThrowIfContainsNull(toolResults);
        ArgumentException.ThrowIfDefaultOrEmpty(toolResults);
        ArgumentException.ThrowIfDuplicateCommittedToolReferences(toolResults);
        ToolResults = toolResults;
    }

    /// <summary>Gets the terminal-record references for which interpretation is pending.</summary>
    /// <value>A nonempty immutable collection with unique correlation, ordered as captured by the session owner.</value>
    public ImmutableArray<CommittedToolResultReference> ToolResults { get; }
}
