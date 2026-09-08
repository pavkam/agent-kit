// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns authoritative grant registration, exact enforcement, use consumption, and revocation state.</summary>
/// <remarks>Implementations must atomically validate and consume uses so concurrent callers cannot exceed <see cref="SecurityGrant.AllowedUses"/>.</remarks>
public interface ISecurityGrantStore
{
    /// <summary>Registers newly issued immutable grant evidence before it can be consumed.</summary>
    /// <param name="grant">The issued grant.</param>
    /// <param name="cancellationToken">Cancels before registration commits.</param>
    /// <returns>A task that completes after registration is authoritative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The identifier is already registered with different evidence.</exception>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default);

    /// <summary>Validates immutable evidence and atomically consumes one matching use immediately before an effect.</summary>
    /// <param name="grant">The presented immutable grant.</param>
    /// <param name="enforcement">Fresh evidence for the concrete effect.</param>
    /// <param name="cancellationToken">Cancels before a use is consumed.</param>
    /// <returns>The terminal validation and consumption result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> or <paramref name="enforcement"/> is null.</exception>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and consumes one exact use while atomically retaining a stable permission-to-start receipt.</summary>
    /// <param name="grant">The presented immutable grant.</param><param name="enforcement">Fresh evidence for the concrete effect.</param><param name="intent">The non-null stable attempt identity and required fence.</param><param name="cancellationToken">Cancels before consumption and receipt persistence commit together.</param>
    /// <returns>The terminal result. <see cref="GrantConsumptionStatus.Consumed"/> carries newly granted permission to start and its authoritative receipt. <see cref="GrantConsumptionStatus.Reconciled"/> carries only historical receipt evidence and never authorizes another effect. An implementation without intent persistence returns an explicit unsuccessful result before consuming a use.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/>, <paramref name="enforcement"/>, or <paramref name="intent"/> is null.</exception>
    /// <exception cref="ArgumentException">The grant or enforcement resources are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Grant or enforcement evidence contains an undefined enum, invalid lifetime, or nonpositive use count.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before atomic consumption and receipt persistence commit.</exception>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new GrantConsumptionResult(
            GrantConsumptionStatus.Unknown,
            0,
            "The grant store does not support atomic enforcement-intent receipts."));
    }

    /// <summary>Idempotently revokes a known grant before future consumption.</summary>
    /// <param name="grantId">The grant to revoke.</param>
    /// <param name="cancellationToken">Cancels before revocation commits.</param>
    /// <returns><see langword="true"/> when the grant exists, including when already revoked.</returns>
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default);
}
