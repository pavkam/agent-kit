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
/// This is a deliberately reduced stand-in for the fuller
/// <c>OutputProcessingRequest</c> described by the structured-output
/// architecture, which additionally carries an <c>AgentRunView</c> and a
/// <c>BudgetExecutionCapability</c>. Until the not-yet-implemented run-view
/// and budget-capability wiring exist at this call site, a caller supplies
/// only the definition, the response, and the current attempt number.
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
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfLessThan(validationAttempt, 1);

        Definition = definition;
        Response = response;
        ValidationAttempt = validationAttempt;
    }

    /// <summary>Gets the output contract to validate against.</summary>
    public OutputDefinition Definition { get; init; }

    /// <summary>Gets the terminal model response to extract a candidate from.</summary>
    public ModelResponse Response { get; init; }

    /// <summary>Gets the one-based attempt number for this processing request.</summary>
    public int ValidationAttempt { get; init; }
}
