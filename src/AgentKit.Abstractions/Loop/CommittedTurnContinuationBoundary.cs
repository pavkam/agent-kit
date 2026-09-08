// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures a complete committed assistant response, its correlated terminal tool evidence, and any output-processing decision.</summary>
/// <remarks>This boundary is used only after a turn is complete. The response and references are immutable evidence for revalidation; the boundary does not itself commit state, produce a history projection, or claim successful settlement.</remarks>
public sealed record CommittedTurnContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes the evidence available after one committed turn.</summary>
    /// <param name="response">The non-null, complete assistant response already associated with the turn.</param>
    /// <param name="toolResults">The non-default immutable references to terminal tool records correlated to calls in <paramref name="response"/>.</param>
    /// <param name="outputDecision">The output processor's decision, or <see langword="null"/> when processing does not apply at this boundary.</param>
    /// <param name="requiresOutputValidation"><see langword="true"/> when successful completion requires a present accepted output decision; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="ArgumentException">The response is incomplete, the array is default or invalid, or correlation is inconsistent.</exception>
    public CommittedTurnContinuationBoundary(
        AssistantMessage response,
        ImmutableArray<CommittedToolResultReference> toolResults,
        OutputProcessingResult? outputDecision,
        bool requiresOutputValidation)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfInvalidCommittedToolReferences(response, toolResults);
        Response = response;
        ToolResults = toolResults;
        OutputDecision = outputDecision;
        RequiresOutputValidation = requiresOutputValidation;
    }

    /// <summary>Gets the complete assistant response captured for the committed turn.</summary>
    /// <value>A non-null immutable response; its completion does not itself establish settlement.</value>
    public AssistantMessage Response { get; }

    /// <summary>Gets the terminal tool-record references correlated to calls in <see cref="Response"/>.</summary>
    /// <value>A non-default immutable array whose correlation was validated at construction.</value>
    public ImmutableArray<CommittedToolResultReference> ToolResults { get; }

    /// <summary>Gets the output processor decision captured at this boundary, when output processing applies.</summary>
    /// <value>A typed decision, or <see langword="null"/> when no output-processing decision was required.</value>
    public OutputProcessingResult? OutputDecision { get; }

    /// <summary>Gets whether a successful completion proposal requires a present accepted output decision.</summary>
    /// <value><see langword="true"/> when missing or non-accepted output processing prevents successful completion; otherwise, <see langword="false"/>.</value>
    public bool RequiresOutputValidation { get; }
}
