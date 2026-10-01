// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the coordinator to delete one memory, committing a tombstone and optionally purging its body.</summary>
/// <remarks>The command carries an operation context rather than a grant: the coordinator selects the authority the context's authorization names, asks for single-use grants bound to the exact store operations, and returns the final receipt.</remarks>
public sealed record MemoryDeleteCommand
{
    /// <summary>Initializes a validated command.</summary>
    /// <param name="context">The operation context the deletion runs under.</param>
    /// <param name="id">The memory to delete.</param>
    /// <param name="expectedVersion">The version the live record must have, or <see langword="null"/> to delete whatever version is stored.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or <paramref name="mode"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A version or key is blank.</exception>
    public MemoryDeleteCommand(MemoryOperationContext context, MemoryId id, VersionToken? expectedVersion, MemoryDeleteMode mode, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        if (expectedVersion is { } version)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(expectedVersion));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        Id = id;
        ExpectedVersion = expectedVersion;
        Mode = mode;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context the deletion runs under.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the memory to delete.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the version the live record must have, or <see langword="null"/>.</summary>
    public VersionToken? ExpectedVersion { get; }

    /// <summary>Gets how far the deletion goes.</summary>
    public MemoryDeleteMode Mode { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
