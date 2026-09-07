// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The candidate failed validation and no further attempt remains.</summary>
public sealed record OutputRejected: OutputProcessingResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputRejected"/> record.</summary>
    /// <param name="failure">Why the candidate failed validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public OutputRejected(OutputValidationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets why the candidate failed validation.</summary>
    public OutputValidationFailure Failure { get; init; }
}
