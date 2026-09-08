// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one registered <see cref="ISessionStore"/> implementation and
/// its capability, consistency, and durability guarantees.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This
/// immutable declaration is captured at composition validation so store
/// selection never probes implementations to infer their capabilities.
/// </remarks>
public sealed record SessionStoreDescriptor
{
    /// <summary>Initializes a declared session-store capability profile.</summary>
    /// <param name="key">The store's registered key.</param>
    /// <param name="capabilities">The optional operations supported by the store.</param>
    /// <param name="consistency">The consistency visible to session callers.</param>
    /// <param name="durable">
    /// <see langword="true"/> if the store persists its record beyond the
    /// current process; <see langword="false"/> for an ephemeral,
    /// in-memory-only store.
    /// </param>
    /// <param name="supportsDistributedFencing">Whether the store can support distributed lane fencing.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capabilities"/> has unknown flags or <paramref name="consistency"/> is undefined.</exception>
    public SessionStoreDescriptor(SessionStoreKey key, SessionStoreCapabilities capabilities,
        SessionConsistencyModel consistency, bool durable, bool supportsDistributedFencing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfUndefined(consistency);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) (capabilities & ~_knownCapabilities), 0U, nameof(capabilities));
        Key = key;
        Capabilities = capabilities;
        Consistency = consistency;
        Durable = durable;
        SupportsDistributedFencing = supportsDistributedFencing;
    }

    private const SessionStoreCapabilities _knownCapabilities =
        SessionStoreCapabilities.Branching | SessionStoreCapabilities.Snapshots |
        SessionStoreCapabilities.Retention | SessionStoreCapabilities.Transactions;

    /// <summary>Gets the store's registered key.</summary>
    /// <exception cref="ArgumentException">The value assigned during initialization or non-destructive mutation is blank.</exception>
    public SessionStoreKey Key
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the optional operations supported by this store.</summary>
    /// <value>Only defined <see cref="SessionStoreCapabilities"/> flags.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value assigned during initialization or non-destructive mutation contains an unknown flag.</exception>
    public SessionStoreCapabilities Capabilities
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) (value & ~_knownCapabilities), 0U, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the consistency guarantee exposed to callers.</summary>
    /// <value>The defined configured consistency model.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value assigned during initialization or non-destructive mutation is undefined.</exception>
    public SessionConsistencyModel Consistency
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the store persists its record beyond
    /// the current process.
    /// </summary>
    public bool Durable { get; init; }

    /// <summary>Gets whether this store can support distributed lane-operation fencing.</summary>
    /// <value><see langword="true"/> only when the store's implementation provides fencing evidence.</value>
    public bool SupportsDistributedFencing { get; init; }
}
