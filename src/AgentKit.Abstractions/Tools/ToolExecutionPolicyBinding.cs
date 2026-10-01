// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one execution-policy reference a run's tool capability may use.</summary>
/// <remarks>A binding is evidence captured with the run's catalog. It is not a policy instance and grants no authority.</remarks>
public sealed record ToolExecutionPolicyBinding
{
    /// <summary>Initializes a binding.</summary>
    /// <param name="reference">The nonnull exact policy reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ToolExecutionPolicyBinding(ToolExecutionPolicyReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <summary>Gets the exact bound reference.</summary>
    public ToolExecutionPolicyReference Reference { get; }
}
