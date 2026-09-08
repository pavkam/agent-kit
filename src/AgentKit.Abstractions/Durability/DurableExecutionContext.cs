// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The captured durability composition one recoverable operation started
/// under, recorded so that recovery rebinds exactly those components instead
/// of whatever the agent is configured with today.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Every key here is resolved again at recovery time and must resolve exactly
/// once. Substituting the agent's current profile would silently reinterpret
/// journaled work under different checkpoint, retry, or fencing rules, and
/// reading evidence from a different journal would report started work as
/// never started. Composition validation therefore rejects a context whose
/// backend, journal, lease manager, or recovery policy is absent rather than
/// falling back to an available alternative.
/// </para>
/// <para>
/// The context holds selection identity only. It never carries live services,
/// scopes, credentials, or a hook dispatch context, because it is serialized
/// into durable records that outlive the process that created them.
/// </para>
/// </remarks>
public sealed record DurableExecutionContext
{
    private readonly SecurityAuthorizationScope _authorization;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableExecutionContext"/> record.
    /// </summary>
    /// <param name="profileKey">The selected durability profile.</param>
    /// <param name="profileVersion">
    /// The published revision of that profile's validated configuration.
    /// </param>
    /// <param name="backendKey">The backend that owns dispatch and handoff.</param>
    /// <param name="journalKey">
    /// The journal that owns checkpoint and terminal truth.
    /// </param>
    /// <param name="leaseManagerKey">
    /// The lease manager that owns distributed ownership and fencing.
    /// </param>
    /// <param name="recoveryPolicyKey">
    /// The policy that classifies evidence into a recovery decision.
    /// </param>
    /// <param name="authorization">
    /// The authorization scope protected durability operations run under.
    /// Journal access, external handoff, and reconciliation are protected
    /// effects and revalidate their grant at the effecting adapter.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profileKey"/>, <paramref name="backendKey"/>,
    /// <paramref name="journalKey"/>, <paramref name="leaseManagerKey"/>,
    /// or <paramref name="recoveryPolicyKey"/> is a default key, or
    /// <paramref name="authorization"/> is <see langword="null"/>.
    /// </exception>
    public DurableExecutionContext(
        DurabilityProfileKey profileKey,
        DurabilityProfileVersion profileVersion,
        DurableBackendKey backendKey,
        DurableJournalKey journalKey,
        DurableLeaseManagerKey leaseManagerKey,
        RecoveryPolicyKey recoveryPolicyKey,
        SecurityAuthorizationScope authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(backendKey.Value, nameof(backendKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(journalKey.Value, nameof(journalKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseManagerKey.Value, nameof(leaseManagerKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryPolicyKey.Value, nameof(recoveryPolicyKey));
        ArgumentNullException.ThrowIfNull(authorization);

        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        BackendKey = backendKey;
        JournalKey = journalKey;
        LeaseManagerKey = leaseManagerKey;
        RecoveryPolicyKey = recoveryPolicyKey;
        _authorization = authorization;
    }

    /// <summary>Gets the selected non-default durability profile.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default key.
    /// </exception>
    public DurabilityProfileKey ProfileKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(ProfileKey));
            field = value;
        }
    }

    /// <summary>Gets the published revision of the selected profile.</summary>
    /// <value>
    /// The captured nonnegative profile revision. Zero is a valid published
    /// revision when the selected profile assigns it.
    /// </value>
    public DurabilityProfileVersion ProfileVersion { get; init; }

    /// <summary>Gets the non-default backend that owns dispatch and handoff.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default key.
    /// </exception>
    public DurableBackendKey BackendKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(BackendKey));
            field = value;
        }
    }

    /// <summary>Gets the non-default journal that owns checkpoint and terminal truth.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default key.
    /// </exception>
    public DurableJournalKey JournalKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(JournalKey));
            field = value;
        }
    }

    /// <summary>Gets the non-default lease manager that owns fencing state.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default key.
    /// </exception>
    public DurableLeaseManagerKey LeaseManagerKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(LeaseManagerKey));
            field = value;
        }
    }

    /// <summary>Gets the non-default policy that classifies recovery evidence.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default key.
    /// </exception>
    public RecoveryPolicyKey RecoveryPolicyKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(RecoveryPolicyKey));
            field = value;
        }
    }

    /// <summary>
    /// Gets the authorization scope protected durability operations run
    /// under.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public SecurityAuthorizationScope Authorization
    {
        get => _authorization;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Authorization));
            _authorization = value;
        }
    }
}
