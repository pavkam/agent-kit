// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Classifies one recoverable operation's durable evidence into a typed
/// recovery decision.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are pure classifiers. A policy MUST NOT start work,
/// dispatch effects, write to the journal, renew leases, or perform I/O other
/// than what its own decision requires. Keeping it side-effect free is what
/// lets the coordinator depend on the policy without the policy depending
/// back on the coordinator.
/// </para>
/// <para>
/// Implementations must be thread-safe and are normally registered as
/// singletons keyed by <see cref="RecoveryPolicyKey"/>.
/// </para>
/// <para>
/// A policy is evaluated against the evidence and descriptor as recorded, not
/// against current configuration. This is why the policy key is captured on
/// the durable context: applying a newer, more permissive policy to older
/// evidence could retry an effect the original configuration deliberately
/// escalated.
/// </para>
/// </remarks>
public interface IRecoveryPolicy
{
    /// <summary>
    /// Decides what to do with one recoverable operation given what is
    /// durably known about it.
    /// </summary>
    /// <param name="operation">
    /// The operation's immutable declaration, including its idempotency
    /// classification and retry ownership.
    /// </param>
    /// <param name="evidence">
    /// What the journal actually recorded, including side-effect certainty.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels the decision. Cancellation produces no decision
    /// and changes no durable state.
    /// </param>
    /// <returns>
    /// One <see cref="RecoveryDecision"/> describing intent. A policy that
    /// cannot justify a safe automatic action returns
    /// <see cref="RecoveryRequiresOperator"/> rather than guessing.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="evidence"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<RecoveryDecision> DecideAsync(
        RecoverableOperationDescriptor operation,
        RecoveryEvidence evidence,
        CancellationToken cancellationToken = default);
}
