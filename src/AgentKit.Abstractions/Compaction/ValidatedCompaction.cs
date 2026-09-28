// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction candidate plus the validation stamp that authorized activation.</summary>
public sealed record ValidatedCompaction
{
    /// <summary>Initializes a new instance of the <see cref="ValidatedCompaction"/> record.</summary>
    /// <param name="candidate">The validated candidate.</param>
    /// <param name="stamp">The validation stamp.</param>
    /// <param name="evidence">Optional bounded validation evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="evidence"/> is a default, uninitialized array.</exception>
    public ValidatedCompaction(
        CompactionCandidate candidate,
        CompactionValidationStamp stamp,
        ImmutableArray<CompactionValidationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfDefault(evidence);
        Candidate = candidate;
        Stamp = stamp;
        Evidence = evidence;
    }

    /// <summary>Gets the validated candidate.</summary>
    public CompactionCandidate Candidate { get; }

    /// <summary>Gets the validation stamp.</summary>
    public CompactionValidationStamp Stamp { get; }

    /// <summary>Gets optional bounded validation evidence.</summary>
    public ImmutableArray<CompactionValidationEvidence> Evidence { get; }
}
