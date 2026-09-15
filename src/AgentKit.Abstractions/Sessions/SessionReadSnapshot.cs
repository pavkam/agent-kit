// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the exact immutable session-branch prefix captured for a paged read.</summary>
/// <remarks>Continuation reads retain this value and exclude later appends beyond <see cref="UpperSequence"/>. Construction alone is not provenance: a store accepts a continuation only when it previously issued the equal snapshot from its serialized read boundary. Adapter-specific bounded retention may expire old continuation evidence, which then fails as unavailable rather than being reinterpreted.</remarks>
public sealed record SessionReadSnapshot
{
    /// <summary>Creates one exact session read boundary.</summary>
    /// <param name="address">The complete session address.</param>
    /// <param name="branchId">The nondefault branch whose prefix was captured.</param>
    /// <param name="version">The exact session version observed when the prefix was captured.</param>
    /// <param name="upperSequence">The inclusive upper sequence of the captured branch prefix.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="branchId"/> is default.</exception>
    public SessionReadSnapshot(SessionAddress address, BranchId branchId, SessionVersion version, SessionSequence upperSequence)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        Address = address;
        BranchId = branchId;
        Version = version;
        UpperSequence = upperSequence;
    }

    /// <summary>Gets the session whose branch prefix was captured.</summary><value>The complete nonnull address.</value>
    public SessionAddress Address { get; }
    /// <summary>Gets the captured branch.</summary><value>A nondefault branch identity.</value>
    public BranchId BranchId { get; }
    /// <summary>Gets the session version observed at capture.</summary><value>The exact nonnegative version.</value>
    public SessionVersion Version { get; }
    /// <summary>Gets the inclusive upper sequence of the captured prefix.</summary><value>Zero for an empty branch; otherwise the last eligible sequence.</value>
    public SessionSequence UpperSequence { get; }
}
