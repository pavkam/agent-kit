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
    /// <param name="maximumSchemaBytes">The maximum canonical UTF-8 size of one schema.</param>
    /// <param name="maximumSchemaDepth">The maximum nested schema depth from 1 through 128.</param>
    /// <param name="maximumSchemaNodes">The maximum values traversed while preflighting one schema.</param>
    /// <param name="maximumCandidateDepth">The maximum nested candidate depth from 1 through 128 evaluated locally.</param>
    /// <param name="maximumCandidateNodes">The maximum values traversed while evaluating one candidate.</param>
    /// <param name="maximumValidationIssues">The maximum number of issues retained by a single validation failure.</param>
    /// <param name="maximumRepairAttempts">The processor-wide ceiling on validation-retry attempts.</param>
    /// <param name="requireSchemaForStructuredModes">Whether structured modes without a declared schema are rejected before extraction.</param>
    /// <param name="allowProviderModeDowngrade">Whether a provider is allowed to downgrade a requested output mode.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any maximum other than <paramref name="maximumRepairAttempts"/> is not positive,
    /// a depth maximum exceeds 128, or
    /// <paramref name="maximumRepairAttempts"/> is negative.
    /// </exception>
    public AgentOutputOptionsSnapshot(
        int maximumCandidateBytes,
        int maximumSchemaBytes,
        int maximumSchemaDepth,
        int maximumSchemaNodes,
        int maximumCandidateDepth,
        int maximumCandidateNodes,
        int maximumValidationIssues,
        int maximumRepairAttempts,
        bool requireSchemaForStructuredModes,
        bool allowProviderModeDowngrade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSchemaBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSchemaDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumSchemaDepth, 128);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSchemaNodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCandidateDepth, 128);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateNodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumValidationIssues);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRepairAttempts);

        MaximumCandidateBytes = maximumCandidateBytes;
        MaximumSchemaBytes = maximumSchemaBytes;
        MaximumSchemaDepth = maximumSchemaDepth;
        MaximumSchemaNodes = maximumSchemaNodes;
        MaximumCandidateDepth = maximumCandidateDepth;
        MaximumCandidateNodes = maximumCandidateNodes;
        MaximumValidationIssues = maximumValidationIssues;
        MaximumRepairAttempts = maximumRepairAttempts;
        RequireSchemaForStructuredModes = requireSchemaForStructuredModes;
        AllowProviderModeDowngrade = allowProviderModeDowngrade;
    }

    /// <summary>Gets the maximum size, in UTF-8 bytes, of an extracted candidate.</summary>
    public int MaximumCandidateBytes { get; }

    /// <summary>Gets the maximum canonical UTF-8 size of one schema.</summary>
    public int MaximumSchemaBytes { get; }

    /// <summary>Gets the maximum nested schema depth.</summary>
    public int MaximumSchemaDepth { get; }

    /// <summary>Gets the maximum values traversed while preflighting one schema.</summary>
    public int MaximumSchemaNodes { get; }

    /// <summary>Gets the maximum nested candidate depth evaluated locally.</summary>
    public int MaximumCandidateDepth { get; }

    /// <summary>Gets the maximum values traversed while evaluating one candidate.</summary>
    public int MaximumCandidateNodes { get; }

    /// <summary>Gets the maximum number of issues retained by a single validation failure.</summary>
    public int MaximumValidationIssues { get; }

    /// <summary>Gets the processor-wide ceiling on validation-retry attempts.</summary>
    public int MaximumRepairAttempts { get; }

    /// <summary>Gets whether structured modes without a declared schema are rejected before extraction.</summary>
    public bool RequireSchemaForStructuredModes { get; }

    /// <summary>Gets whether a provider is allowed to downgrade a requested output mode.</summary>
    public bool AllowProviderModeDowngrade { get; }
}
