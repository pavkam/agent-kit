// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable evidence that one candidate passed validation.</summary>
public sealed record CompactionValidationStamp
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidationStamp"/> record.</summary>
    /// <param name="validatorVersion">The validator version that produced the stamp.</param>
    /// <param name="candidateHash">A hash of the validated candidate at validation time.</param>
    /// <param name="validatedAt">When validation completed.</param>
    public CompactionValidationStamp(
        CompactionValidatorVersion validatorVersion,
        ContentHash candidateHash,
        DateTimeOffset validatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(validatorVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(candidateHash, default);
        ValidatorVersion = validatorVersion;
        CandidateHash = candidateHash;
        ValidatedAt = validatedAt;
    }

    /// <summary>Gets the validator version that produced the stamp.</summary>
    public CompactionValidatorVersion ValidatorVersion { get; }

    /// <summary>Gets a hash of the validated candidate at validation time.</summary>
    public ContentHash CandidateHash { get; }

    /// <summary>Gets when validation completed.</summary>
    public DateTimeOffset ValidatedAt { get; }
}
