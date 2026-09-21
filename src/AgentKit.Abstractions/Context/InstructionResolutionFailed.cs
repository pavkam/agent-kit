// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Instruction resolution failed before any instruction message was committed to the request.</summary>
public sealed record InstructionResolutionFailed: InstructionResolutionResult
{
    /// <summary>Initializes a failed resolution.</summary>
    /// <param name="failure">The typed preparation failure describing why resolution stopped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public InstructionResolutionFailed(ContextPreparationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the typed preparation failure describing why resolution stopped.</summary>
    public ContextPreparationFailure Failure { get; }
}
