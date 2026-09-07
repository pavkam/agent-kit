// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The validator raised no issue against the candidate.</summary>
public sealed record OutputValidationPassed: OutputValidationResult
{
    /// <summary>Gets the shared instance representing an unconditional pass.</summary>
    public static OutputValidationPassed Instance { get; } = new();
}
