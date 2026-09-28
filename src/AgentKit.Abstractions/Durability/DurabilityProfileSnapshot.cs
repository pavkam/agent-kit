// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An immutable named durability profile resolved from composition-time registration.</summary>
/// <remarks>
/// A profile names the backend, journal, lease manager, and recovery policy an agent definition selects, plus the
/// recoverable operations it enables. It resolves no service, reserves no ownership, and never selects a persistence
/// target on a caller's behalf. Recovery reads the profile captured in a persisted
/// <see cref="DurableExecutionContext"/> rather than the agent's latest registration.
/// </remarks>
public sealed record DurabilityProfileSnapshot
{
    /// <summary>Initializes an immutable durability profile snapshot.</summary>
    /// <param name="key">The nondefault, non-blank profile key.</param>
    /// <param name="version">The positive published profile revision.</param>
    /// <param name="backendKey">The nondefault durable backend key this profile selects.</param>
    /// <param name="journalKey">The nondefault durable journal key this profile selects.</param>
    /// <param name="leaseManagerKey">The nondefault durable lease-manager key this profile selects.</param>
    /// <param name="recoveryPolicyKey">The nondefault recovery-policy key this profile selects.</param>
    /// <param name="enabledOperations">The initialized, possibly empty set of recoverable operation names this profile enables.</param>
    /// <param name="configurationFingerprint">The non-blank fingerprint of the captured profile configuration.</param>
    /// <exception cref="ArgumentException">
    /// A key is blank, <paramref name="enabledOperations"/> is a default array, or
    /// <paramref name="configurationFingerprint"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public DurabilityProfileSnapshot(
        DurabilityProfileKey key,
        DurabilityProfileVersion version,
        DurableBackendKey backendKey,
        DurableJournalKey journalKey,
        DurableLeaseManagerKey leaseManagerKey,
        RecoveryPolicyKey recoveryPolicyKey,
        ImmutableArray<DurableOperationName> enabledOperations,
        ContentHash configurationFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(backendKey.Value, nameof(backendKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(journalKey.Value, nameof(journalKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseManagerKey.Value, nameof(leaseManagerKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryPolicyKey.Value, nameof(recoveryPolicyKey));
        ArgumentException.ThrowIfDefault(enabledOperations);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFingerprint.Value, nameof(configurationFingerprint));

        Key = key;
        Version = version;
        BackendKey = backendKey;
        JournalKey = journalKey;
        LeaseManagerKey = leaseManagerKey;
        RecoveryPolicyKey = recoveryPolicyKey;
        EnabledOperations = enabledOperations;
        ConfigurationFingerprint = configurationFingerprint;
    }

    /// <summary>Gets the profile key an agent definition selects.</summary>
    public DurabilityProfileKey Key { get; }

    /// <summary>Gets the positive published profile revision.</summary>
    /// <value>The revision recovery must match before it reuses this profile's component selection.</value>
    public DurabilityProfileVersion Version { get; }

    /// <summary>Gets the durable backend key this profile selects.</summary>
    public DurableBackendKey BackendKey { get; }

    /// <summary>Gets the durable journal key this profile selects.</summary>
    public DurableJournalKey JournalKey { get; }

    /// <summary>Gets the durable lease-manager key this profile selects.</summary>
    public DurableLeaseManagerKey LeaseManagerKey { get; }

    /// <summary>Gets the recovery-policy key this profile selects.</summary>
    public RecoveryPolicyKey RecoveryPolicyKey { get; }

    /// <summary>Gets the recoverable operation names this profile enables.</summary>
    /// <value>An empty set means the profile enables no durable operation, which is a valid but inert configuration.</value>
    public ImmutableArray<DurableOperationName> EnabledOperations { get; }

    /// <summary>Gets the fingerprint of the captured profile configuration.</summary>
    /// <value>A deterministic digest over the profile's keys, version, and enabled operations.</value>
    public ContentHash ConfigurationFingerprint { get; }
}
