// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Mutable configuration for one named durability profile.</summary>
/// <remarks>
/// <para>
/// A profile names the exact keyed components an operation runs under. Those keys are captured into every
/// <see cref="DurableExecutionContext"/> the profile produces and are persisted with the operation, so recovery
/// activates the composition the work actually started under rather than whatever the profile names today.
/// </para>
/// <para>
/// These options are mutated only while the service collection is being configured. Every required key must be set by
/// the time the provider is built; the registry validates the accumulated result and refuses a profile that leaves one
/// unset, because a half-selected profile would otherwise fail in the middle of an operation.
/// </para>
/// <para>
/// Instances are not thread-safe and are never mutated after composition.
/// </para>
/// </remarks>
public sealed class DurabilityProfileOptions
{
    /// <summary>Gets or sets the published revision of this profile's selection.</summary>
    /// <value>
    /// A positive revision recorded with every operation so a resumed operation can be recognized as belonging to an
    /// earlier published selection. Defaults to <c>1</c>.
    /// </value>
    public DurabilityProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the key of the durable backend that may own handoff and reconciliation.</summary>
    /// <value>
    /// The exact key of a registered <see cref="IDurableExecutionBackend"/>. The backend's own capability claims, not
    /// this selection, decide whether handoff or reconciliation is actually attempted.
    /// </value>
    public DurableBackendKey BackendKey { get; set; }

    /// <summary>Gets or sets the key of the journal that holds this profile's authoritative durable records.</summary>
    /// <value>
    /// The exact key of a registered <see cref="IDurableOperationJournal"/>. Journal access is protected, so the
    /// selected journal also determines which grant audience every durable write must be authorized for.
    /// </value>
    public DurableJournalKey JournalKey { get; set; }

    /// <summary>Gets or sets the key of the lease manager that allocates this profile's ownership generations.</summary>
    /// <value>
    /// The exact key of a registered <see cref="IDurableLeaseManager"/>. A process-local lease manager yields tokens
    /// that are authoritative only within one process, whatever the journal can persist.
    /// </value>
    public DurableLeaseManagerKey LeaseManagerKey { get; set; }

    /// <summary>Gets or sets the key of the policy that classifies this profile's recovery decisions.</summary>
    /// <value>
    /// The exact key of a registered <see cref="IRecoveryPolicy"/>. The policy decides between start, retry,
    /// reconciliation, commit, and operator action; the coordinator performs only what it returns.
    /// </value>
    public RecoveryPolicyKey RecoveryPolicyKey { get; set; }

    /// <summary>Gets the mutable set of recoverable operation names this profile enables.</summary>
    /// <value>
    /// A live list whose entries must each carry name text. An empty list places no restriction on operation names.
    /// Order is not significant: the projected snapshot's configuration fingerprint sorts the names so that two
    /// equivalent selections registered in different orders produce the same fingerprint.
    /// </value>
    public List<DurableOperationName> EnabledOperations { get; } = [];
}
