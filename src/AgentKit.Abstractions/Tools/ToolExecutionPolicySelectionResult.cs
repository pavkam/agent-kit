// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed selected and unavailable outcomes of one <see cref="IToolExecutionPolicySelector"/> lookup.</summary>
/// <remarks>Selection is by exact key and version. An absent policy is a typed outcome and never falls back to a default.</remarks>
public abstract record ToolExecutionPolicySelectionResult
{
    /// <summary>Initializes one of the two supported selection outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed selection family.</exception>
    private protected ToolExecutionPolicySelectionResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolExecutionPolicySelected or ToolExecutionPolicyUnavailable, true, "result");

    /// <summary>Copies the base state of a supported immutable selection outcome.</summary>
    /// <param name="original">The nonnull original result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed selection family.</exception>
    protected ToolExecutionPolicySelectionResult(ToolExecutionPolicySelectionResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolExecutionPolicySelected or ToolExecutionPolicyUnavailable, true, "result");
    }
}
