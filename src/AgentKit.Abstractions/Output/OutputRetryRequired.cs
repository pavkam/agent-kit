// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The candidate failed validation, but another attempt remains within the definition's retry policy.</summary>
/// <remarks>
/// This outcome never calls the provider, loop, or publisher itself. The
/// loop decides whether another model request is actually allowed, for
/// example against its own turn limit.
/// </remarks>
public sealed record OutputRetryRequired: OutputProcessingResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputRetryRequired"/> record.</summary>
    /// <param name="repair">Bounded, safe guidance for the retry attempt.</param>
    /// <param name="failure">Why the candidate failed validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="repair"/> or <paramref name="failure"/> is null.</exception>
    public OutputRetryRequired(OutputRepairInstruction repair, OutputValidationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(repair);
        ArgumentNullException.ThrowIfNull(failure);

        Repair = repair;
        Failure = failure;
    }

    /// <summary>Gets bounded, safe guidance for the retry attempt.</summary>
    public OutputRepairInstruction Repair { get; init; }

    /// <summary>Gets why the candidate failed validation.</summary>
    public OutputValidationFailure Failure { get; init; }
}
