// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Implements only legacy consumption so reusable tests can prove intent-aware default behavior is fail-closed.</summary>
internal sealed class LegacyOnlySecurityGrantStore: ISecurityGrantStore
{
    /// <summary>Gets the number of legacy consumption effects invoked by the conformance case.</summary>
    /// <value>Zero unless the intent-aware default incorrectly delegates to the legacy operation.</value>
    internal int LegacyConsumptionCalls { get; private set; }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        LegacyConsumptionCalls++;
        return ValueTask.FromResult(new GrantConsumptionResult(
            GrantConsumptionStatus.Consumed, 0, "Legacy consumption was invoked."));
    }

    /// <inheritdoc/>
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);
}
