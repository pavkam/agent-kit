// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request to run a single named <see cref="IOutputValidator"/> against an already schema-valid candidate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// validator receives the candidate only after protocol, parse, and schema
/// validation already succeeded; it may accept the candidate or report
/// issues, but never performs a hidden side effect.
/// </remarks>
public sealed record OutputValidationRequest
{
    /// <summary>Initializes a new instance of the <see cref="OutputValidationRequest"/> record.</summary>
    /// <param name="definition">The output contract the candidate is being validated against.</param>
    /// <param name="candidate">The schema-valid candidate to validate.</param>
    /// <param name="validationAttempt">The one-based attempt number for this validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="candidate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="validationAttempt"/> is less than one.</exception>
    public OutputValidationRequest(OutputDefinition definition, ValidatedOutput candidate, int validationAttempt)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfLessThan(validationAttempt, 1);

        Definition = definition;
        Candidate = candidate;
        ValidationAttempt = validationAttempt;
    }

    /// <summary>Gets the output contract the candidate is being validated against.</summary>
    public OutputDefinition Definition { get; init; }

    /// <summary>Gets the schema-valid candidate to validate.</summary>
    public ValidatedOutput Candidate { get; init; }

    /// <summary>Gets the one-based attempt number for this validation.</summary>
    public int ValidationAttempt { get; init; }
}
