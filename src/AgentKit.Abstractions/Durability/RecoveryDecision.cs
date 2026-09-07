// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for what a recovery policy decided to do with one
/// recoverable operation after examining its durable evidence.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="RecoveryStartOperation"/>,
/// <see cref="RecoveryReconcileOperation"/>,
/// <see cref="RecoveryRetryOperation"/>,
/// <see cref="RecoveryCommitRecordedResult"/>,
/// <see cref="RecoveryRequiresOperator"/>, and
/// <see cref="RecoveryNotPossible"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a seventh kind and bypass exhaustive
/// handling.
/// </para>
/// <para>
/// A decision describes intent only. The policy that produced it never
/// executes the operation, which keeps classification independent of the
/// coordinator that acts on it.
/// </para>
/// </remarks>
public abstract record RecoveryDecision
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryDecision"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    private protected RecoveryDecision()
    {
    }
}
