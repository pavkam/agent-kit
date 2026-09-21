// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Forwards to an authoritative grant store and lets a test alter only its intent-aware returned evidence.</summary>
internal sealed class InterceptingSecurityGrantStore(ISecurityGrantStore inner): ISecurityGrantStore
{
    private readonly ISecurityGrantStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>Gets or sets the synchronous result interceptor invoked after authoritative intent consumption.</summary>
    /// <value>A test callback that may replace returned evidence or cancel a caller token; null preserves the exact result.</value>
    internal Func<
        GrantConsumptionResult,
        SecurityEnforcementRequest,
        SecurityEnforcementIntent,
        GrantConsumptionResult>? IntentResultInterceptor
    { get; set; }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
        _inner.RegisterAsync(grant, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default) =>
        _inner.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        return IntentResultInterceptor?.Invoke(result, enforcement, intent) ?? result;
    }

    /// <inheritdoc/>
    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
    }
}
