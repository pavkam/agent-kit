// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns authoritative grant registration, exact enforcement, use consumption, and revocation state.</summary>
/// <remarks>Implementations must atomically validate and consume uses, and persist the permission-to-start receipt in the same transition, so concurrent callers cannot exceed <see cref="SecurityGrant.AllowedUses"/> and an uncertain acknowledgement can be reconciled by intent identity.</remarks>
public interface ISecurityGrantStore
{
    /// <summary>Registers newly issued immutable grant evidence before it can be consumed.</summary>
    /// <param name="grant">The issued grant.</param>
    /// <param name="cancellationToken">Cancels before registration commits.</param>
    /// <returns>A task that completes after registration is authoritative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The identifier is already registered with different evidence.</exception>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default);

    /// <summary>Validates and consumes one exact use while atomically retaining a stable permission-to-start receipt.</summary>
    /// <param name="grant">The presented immutable grant.</param>
    /// <param name="enforcement">Fresh evidence for the concrete effect.</param>
    /// <param name="intent">The non-null stable attempt identity and required fence.</param>
    /// <param name="cancellationToken">Cancels before consumption and receipt persistence commit together.</param>
    /// <returns>The terminal result. <see cref="GrantConsumptionStatus.Consumed"/> carries newly granted permission to start and its authoritative receipt. <see cref="GrantConsumptionStatus.Reconciled"/> carries only historical receipt evidence and never authorizes another effect. Every other status carries no receipt and consumes nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/>, <paramref name="enforcement"/>, or <paramref name="intent"/> is null.</exception>
    /// <exception cref="ArgumentException">The grant or enforcement resources are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Grant or enforcement evidence contains an undefined enum, invalid lifetime, or nonpositive use count.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before atomic consumption and receipt persistence commit.</exception>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes one registered grant with an explicit recorded reason.</summary>
    /// <param name="grantId">The grant to revoke.</param>
    /// <param name="reason">The non-sensitive revocation reason retained with the store transition.</param>
    /// <param name="cancellationToken">Cancels before revocation commits.</param>
    /// <returns>A closed revocation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grantId"/> is default.</exception>
    public ValueTask<GrantRevocationResult> RevokeAsync(
        GrantId grantId,
        RevocationReason reason,
        CancellationToken cancellationToken = default);
}
