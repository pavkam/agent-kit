// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>An immutable, validated copy of <see cref="AgentOutputOptions"/> captured at composition time.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
internal sealed record AgentOutputOptionsSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="AgentOutputOptionsSnapshot"/> record.</summary>
    /// <param name="maximumCandidateBytes">The maximum size, in UTF-8 bytes, of an extracted candidate.</param>
    /// <param name="maximumValidationIssues">The maximum number of issues retained by a single validation failure.</param>
    /// <param name="maximumRepairAttempts">The processor-wide ceiling on validation-retry attempts.</param>
    /// <param name="requireSchemaForStructuredModes">Whether structured modes without a declared schema are rejected before extraction.</param>
    /// <param name="allowProviderModeDowngrade">Whether a provider is allowed to downgrade a requested output mode.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumCandidateBytes"/> or <paramref name="maximumValidationIssues"/> is not positive, or
    /// <paramref name="maximumRepairAttempts"/> is negative.
    /// </exception>
    public AgentOutputOptionsSnapshot(
        int maximumCandidateBytes,
        int maximumValidationIssues,
        int maximumRepairAttempts,
        bool requireSchemaForStructuredModes,
        bool allowProviderModeDowngrade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumValidationIssues);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRepairAttempts);

        MaximumCandidateBytes = maximumCandidateBytes;
        MaximumValidationIssues = maximumValidationIssues;
        MaximumRepairAttempts = maximumRepairAttempts;
        RequireSchemaForStructuredModes = requireSchemaForStructuredModes;
        AllowProviderModeDowngrade = allowProviderModeDowngrade;
    }

    /// <summary>Gets the maximum size, in UTF-8 bytes, of an extracted candidate.</summary>
    public int MaximumCandidateBytes { get; }

    /// <summary>Gets the maximum number of issues retained by a single validation failure.</summary>
    public int MaximumValidationIssues { get; }

    /// <summary>Gets the processor-wide ceiling on validation-retry attempts.</summary>
    public int MaximumRepairAttempts { get; }

    /// <summary>Gets whether structured modes without a declared schema are rejected before extraction.</summary>
    public bool RequireSchemaForStructuredModes { get; }

    /// <summary>Gets whether a provider is allowed to downgrade a requested output mode.</summary>
    public bool AllowProviderModeDowngrade { get; }
}
