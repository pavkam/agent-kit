// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of <see cref="DurableExecutionContext"/>, the durability composition one operation was accepted under.</summary>
/// <remarks>
/// <para>
/// Recovery rebinds exactly these keys rather than the agent's current profile, so every key is persisted and rebuilt
/// through its own validating constructor. A blank persisted key fails on read instead of silently resolving whichever
/// registration happens to be available.
/// </para>
/// <para>
/// The captured authorization reuses <see cref="JsonSecurityAuthorizationContext"/>, the single portable evidence shape
/// shared by every first-party durable store, so authorization captured by one adapter reads back through another. The
/// document carries evidence only; it holds no grant, credential, or live service.
/// </para>
/// </remarks>
/// <param name="ProfileKey">The non-blank selected durability profile key.</param>
/// <param name="ProfileVersion">The captured published revision of that profile.</param>
/// <param name="BackendKey">The non-blank backend key that owns dispatch and handoff.</param>
/// <param name="JournalKey">The non-blank journal key that owns checkpoint and terminal truth.</param>
/// <param name="LeaseManagerKey">The non-blank lease-manager key that owns ownership and fencing.</param>
/// <param name="RecoveryPolicyKey">The non-blank policy key that classifies recovery evidence.</param>
/// <param name="Authorization">The non-null captured authorization evidence protected durability work runs under.</param>
internal sealed record DurableExecutionContextDocument(
    string ProfileKey,
    long ProfileVersion,
    string BackendKey,
    string JournalKey,
    string LeaseManagerKey,
    string RecoveryPolicyKey,
    JsonSecurityAuthorizationContext Authorization)
{
    /// <summary>Projects one domain durability context into its portable persisted representation.</summary>
    /// <param name="value">The non-null captured context to project.</param>
    /// <returns>A document carrying every unwrapped key plus the projected authorization evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static DurableExecutionContextDocument FromDomain(DurableExecutionContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DurableExecutionContextDocument(
            value.ProfileKey.Value,
            value.ProfileVersion.Value,
            value.BackendKey.Value,
            value.JournalKey.Value,
            value.LeaseManagerKey.Value,
            value.RecoveryPolicyKey.Value,
            JsonSecurityAuthorizationContext.FromDomain(value.Authorization));
    }

    /// <summary>Reconstructs the exact domain durability context this document was projected from.</summary>
    /// <returns>A context equal to the projected original, including its nested authorization evidence.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Authorization"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">A persisted key is null, empty, or whitespace, or the nested authorization carries invalid evidence.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The nested authorization carries an out-of-range revision or identity.</exception>
    internal DurableExecutionContext ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Authorization);
        return new DurableExecutionContext(
            new DurabilityProfileKey(ProfileKey),
            new DurabilityProfileVersion(ProfileVersion),
            new DurableBackendKey(BackendKey),
            new DurableJournalKey(JournalKey),
            new DurableLeaseManagerKey(LeaseManagerKey),
            new RecoveryPolicyKey(RecoveryPolicyKey),
            Authorization.ToDomain());
    }
}
