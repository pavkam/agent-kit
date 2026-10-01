// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no policy is available under the exact requested reference.</summary>
public sealed record ToolExecutionPolicyUnavailable: ToolExecutionPolicySelectionResult
{
    /// <summary>Initializes an unavailable outcome that retains the requested reference.</summary>
    /// <param name="reference">The nonnull reference that could not be selected.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ToolExecutionPolicyUnavailable(ToolExecutionPolicyReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <summary>Gets the unavailable reference.</summary>
    public ToolExecutionPolicyReference Reference { get; }
}
