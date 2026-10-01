// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects the registered <see cref="IToolExecutionPolicy"/> for one exact captured policy reference.</summary>
/// <remarks>
/// Selection never falls back to a default policy, a newer revision, or an unkeyed registration. The capability's
/// <see cref="ToolExecutionCapability.ExecutionPolicies"/> bindings bound which references the run may use.
/// </remarks>
public interface IToolExecutionPolicySelector
{
    /// <summary>Selects the policy registered under <paramref name="reference"/>.</summary>
    /// <param name="reference">The nonnull exact reference captured with the resolved tool.</param>
    /// <param name="capability">The nonnull invocation capability whose policy bindings must include <paramref name="reference"/>.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns><see cref="ToolExecutionPolicySelected"/> or <see cref="ToolExecutionPolicyUnavailable"/> for the exact reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="capability"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ToolExecutionPolicySelectionResult> SelectAsync(
        ToolExecutionPolicyReference reference,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default);
}
