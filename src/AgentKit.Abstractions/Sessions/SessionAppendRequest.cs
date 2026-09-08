// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request to append one or more entries to a session branch, guarded by
/// an expected optimistic-concurrency version.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. All entries in one request commit atomically
/// against <see cref="ExpectedVersion"/>: either every entry is appended and
/// the canonical session advances to one new version, or none are appended and the
/// caller observes a conflict.
/// </remarks>
public sealed record SessionAppendRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionAppendRequest"/> record.</summary>
    /// <param name="context">The operation context for this append.</param>
    /// <param name="branchId">The branch to append to.</param>
    /// <param name="expectedVersion">
    /// The canonical whole-session version the caller last observed.
    /// The append is rejected with a conflict if the session has since
    /// advanced.
    /// </param>
    /// <param name="idempotencyKey">
    /// The key that makes repeating this exact request safe: a retry with
    /// the same key never duplicates the appended entries.
    /// </param>
    /// <param name="entries">The entries to append, in commit order.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is a default, uninitialized array, or is
    /// empty.
    /// </exception>
    public SessionAppendRequest(
        SessionOperationContext context,
        BranchId branchId,
        SessionVersion expectedVersion,
        IdempotencyKey idempotencyKey,
        ImmutableArray<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefaultOrEmpty(entries);
        ArgumentException.ThrowIfContainsNull(entries);

        Context = context;
        BranchId = branchId;
        ExpectedVersion = expectedVersion;
        IdempotencyKey = idempotencyKey;
        Entries = entries;
    }

    /// <summary>Gets the operation context for this append.</summary>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the branch to append to.</summary>
    public BranchId BranchId { get; }

    /// <summary>
    /// Gets the canonical whole-session version the caller last observed.
    /// </summary>
    public SessionVersion ExpectedVersion { get; }

    /// <summary>
    /// Gets the key that makes repeating this exact request safe.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the entries to append, in commit order.</summary>
    public ImmutableArray<SessionEntry> Entries { get; }

    /// <inheritdoc/>
    public bool Equals(SessionAppendRequest? other) =>
        other is not null
        && Context.Equals(other.Context)
        && BranchId.Equals(other.BranchId)
        && ExpectedVersion.Equals(other.ExpectedVersion)
        && IdempotencyKey.Equals(other.IdempotencyKey)
        && Entries.SequenceEqual(other.Entries);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Context);
        hash.Add(BranchId);
        hash.Add(ExpectedVersion);
        hash.Add(IdempotencyKey);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
