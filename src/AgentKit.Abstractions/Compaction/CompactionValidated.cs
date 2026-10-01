// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A candidate-validation attempt that found no issues.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionValidated: CompactionValidationResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidated"/> record.</summary>
    /// <param name="compaction">The validated compaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="compaction"/> is null.</exception>
    public CompactionValidated(ValidatedCompaction compaction)
    {
        ArgumentNullException.ThrowIfNull(compaction);
        Compaction = compaction;
    }

    /// <summary>Gets the validated compaction.</summary>
    public ValidatedCompaction Compaction { get; init; }

    /// <summary>Gets the validated candidate.</summary>
    public CompactionCandidate Candidate => Compaction.Candidate;
}
