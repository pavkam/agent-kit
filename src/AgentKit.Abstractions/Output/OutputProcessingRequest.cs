// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to extract and validate a terminal model response against an output definition.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The structured-output architecture also names an <c>AgentRunView</c> on this
/// request; that view has not landed. Callers supply the selected model descriptor,
/// optional budget capability, and validation attempt explicitly instead.
/// </para>
/// </remarks>
public sealed record OutputProcessingRequest
{
    /// <summary>Initializes a new instance of the <see cref="OutputProcessingRequest"/> record.</summary>
    /// <param name="definition">The output contract to validate against.</param>
    /// <param name="response">The terminal model response to extract a candidate from.</param>
    /// <param name="validationAttempt">The one-based attempt number for this processing request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="response"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="validationAttempt"/> is less than one.</exception>
    public OutputProcessingRequest(OutputDefinition definition, ModelResponse response, int validationAttempt)
        : this(definition, response, validationAttempt, model: null, budget: null)
    {
    }

    /// <summary>Initializes a request with model capability evidence and an optional repair budget capability.</summary>
    /// <param name="definition">The output contract to validate against.</param>
    /// <param name="response">The terminal model response to extract a candidate from.</param>
    /// <param name="validationAttempt">The one-based attempt number for this processing request.</param>
    /// <param name="model">The model that produced the response, when known.</param>
    /// <param name="budget">The borrowed budget capability for repair reservations, when available.</param>
    /// <param name="agentId">The agent identity for hook dispatch, when available.</param>
    /// <param name="sessionId">The session identity for hook dispatch, when available.</param>
    /// <param name="turn">The one-based turn number for hook dispatch, or zero when unknown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="response"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="validationAttempt"/> is less than one, or <paramref name="turn"/> is negative.</exception>
    public OutputProcessingRequest(
        OutputDefinition definition,
        ModelResponse response,
        int validationAttempt,
        ModelDescriptor? model,
        BudgetExecutionCapability? budget,
        AgentId agentId = default,
        SessionId sessionId = default,
        int turn = 0)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfLessThan(validationAttempt, 1);
        if (turn != 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turn);
        }

        Definition = definition;
        Response = response;
        ValidationAttempt = validationAttempt;
        Model = model;
        Budget = budget;
        AgentId = agentId;
        SessionId = sessionId;
        Turn = turn;
    }

    /// <summary>Gets the output contract to validate against.</summary>
    public OutputDefinition Definition { get; init; }

    /// <summary>Gets the terminal model response to extract a candidate from.</summary>
    public ModelResponse Response { get; init; }

    /// <summary>Gets the one-based attempt number for this processing request.</summary>
    public int ValidationAttempt { get; init; }

    /// <summary>Gets the model that produced the response, when the loop supplied one.</summary>
    public ModelDescriptor? Model { get; init; }

    /// <summary>Gets the borrowed budget capability for output-repair reservations, when the run is budgeted.</summary>
    public BudgetExecutionCapability? Budget { get; init; }

    /// <summary>Gets the agent identity when the loop supplied hook context.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session identity when the loop supplied hook context.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the one-based turn number when the loop supplied hook context; otherwise zero.</summary>
    public int Turn { get; init; }
}
