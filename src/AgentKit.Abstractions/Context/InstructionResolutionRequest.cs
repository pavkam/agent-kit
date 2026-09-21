// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable evidence one instruction resolution pass evaluates.</summary>
public sealed record InstructionResolutionRequest
{
    /// <summary>Initializes one instruction resolution request.</summary>
    /// <param name="sources">The declared instruction sources to resolve for one model request.</param>
    /// <param name="runId">The active run identifier.</param>
    /// <param name="turnId">The active turn identifier.</param>
    /// <param name="modelRequestId">The model request being prepared.</param>
    /// <exception cref="ArgumentException"><paramref name="sources"/> is a default array or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required identity is default.</exception>
    public InstructionResolutionRequest(
        ImmutableArray<InstructionSource> sources,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId)
    {
        ArgumentException.ThrowIfContainsNull(sources);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);

        Sources = sources;
        RunId = runId;
        TurnId = turnId;
        ModelRequestId = modelRequestId;
    }

    /// <summary>Gets the declared instruction sources to resolve for one model request.</summary>
    public ImmutableArray<InstructionSource> Sources { get; }

    /// <summary>Gets the active run identifier.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the active turn identifier.</summary>
    public TurnId TurnId { get; }

    /// <summary>Gets the model request being prepared.</summary>
    public ModelRequestId ModelRequestId { get; }
}
