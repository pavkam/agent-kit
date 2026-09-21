// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one instruction resolution attempt.</summary>
/// <remarks>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="IInstructionResolver"/> implementations and their callers.
/// </remarks>
public abstract record InstructionResolutionResult
{
    /// <summary>Initializes a closed instruction-resolution outcome.</summary>
    private protected InstructionResolutionResult()
    {
    }
}
