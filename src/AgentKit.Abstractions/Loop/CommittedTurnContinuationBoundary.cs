// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures a complete assistant response and any correlated committed terminal tool records.</summary>
public sealed record CommittedTurnContinuationBoundary: RunContinuationBoundary
{
    /// <summary>Initializes a committed-turn boundary.</summary>
    /// <param name="response">The complete committed assistant response.</param>
    /// <param name="toolResults">References to committed terminal tool records for calls in <paramref name="response"/>.</param>
    /// <param name="outputDecision">The processor decision when output validation applies.</param>
    /// <param name="requiresOutputValidation">Whether completion requires a present accepted output decision.</param>
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

    /// <summary>Gets the complete assistant response.</summary>
    public AssistantMessage Response { get; }

    /// <summary>Gets references to terminal tool records whose commit is established by the session owner.</summary>
    public ImmutableArray<CommittedToolResultReference> ToolResults { get; }

    /// <summary>Gets the output decision when processing applies.</summary>
    public OutputProcessingResult? OutputDecision { get; }

    /// <summary>Gets whether successful completion requires accepted output.</summary>
    public bool RequiresOutputValidation { get; }
}
