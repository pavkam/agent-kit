// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Every entry in the request committed atomically and the branch advanced
/// to <see cref="NewVersion"/>.
/// </summary>
public sealed record SessionAppended: SessionAppendResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionAppended"/> record.</summary>
    /// <param name="newVersion">The branch's new version after this append.</param>
    /// <param name="committedEntries">
    /// The committed entries, in the same order as the request. A retried
    /// append with the same idempotency key returns the originally
    /// committed entries rather than appending duplicates.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="committedEntries"/> is a default, uninitialized
    /// array.
    /// </exception>
    public SessionAppended(SessionVersion newVersion, ImmutableArray<SessionEntry> committedEntries)
    {
        ArgumentException.ThrowIfDefault(committedEntries);
        NewVersion = newVersion;
        CommittedEntries = committedEntries;
    }

    /// <summary>Gets the branch's new version after this append.</summary>
    public SessionVersion NewVersion { get; init; }

    /// <summary>Gets the committed entries, in the same order as the request.</summary>
    public ImmutableArray<SessionEntry> CommittedEntries { get; init; }

    /// <inheritdoc/>
    public bool Equals(SessionAppended? other) =>
        other is not null && NewVersion.Equals(other.NewVersion) && CommittedEntries.SequenceEqual(other.CommittedEntries);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NewVersion);
        foreach (var entry in CommittedEntries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
