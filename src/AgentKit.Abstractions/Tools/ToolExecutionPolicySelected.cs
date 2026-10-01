// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers the policy registered under the exact requested reference.</summary>
public sealed record ToolExecutionPolicySelected: ToolExecutionPolicySelectionResult
{
    /// <summary>Wraps the selected policy.</summary>
    /// <param name="policy">The nonnull policy whose <see cref="IToolExecutionPolicy.Reference"/> is the requested reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public ToolExecutionPolicySelected(IToolExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;
    }

    /// <summary>Gets the selected policy.</summary>
    /// <value>A borrowed policy instance; the selector, not the caller, owns its lifetime.</value>
    public IToolExecutionPolicy Policy { get; }
}
