// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Implements only the legacy consumption operation so tests can prove the intent-aware default fails closed.</summary>
internal sealed class LegacyOnlySecurityGrantStore: ISecurityGrantStore
{
    /// <summary>Gets the number of legacy consumption effects invoked by the test.</summary>
    /// <value>Zero unless a caller incorrectly delegates intent-aware consumption to the legacy operation.</value>
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
    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
    }
}
