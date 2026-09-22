// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one successful file write with committed byte and fingerprint evidence.</summary>
public sealed record FileWriteSuccess: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteSuccess"/> record.</summary>
    /// <param name="outcome">Which successful disposition outcome committed.</param>
    /// <param name="payloadBytes">The number of payload bytes consumed.</param>
    /// <param name="previousBytes">The previous target size before the write.</param>
    /// <param name="finalBytes">The final committed target size.</param>
    /// <param name="payloadFingerprint">The fingerprint of consumed payload bytes.</param>
    /// <param name="finalFingerprint">The fingerprint of the committed final file.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any byte count is negative.
    /// </exception>
    public FileWriteSuccess(
        FileWriteOutcomeKind outcome,
        long payloadBytes,
        long previousBytes,
        long finalBytes,
        ContentHash payloadFingerprint,
        ContentHash finalFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(previousBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(finalBytes);
        Outcome = outcome;
        PayloadBytes = payloadBytes;
        PreviousBytes = previousBytes;
        FinalBytes = finalBytes;
        PayloadFingerprint = payloadFingerprint;
        FinalFingerprint = finalFingerprint;
    }

    /// <summary>Gets which successful disposition outcome committed.</summary>
    public FileWriteOutcomeKind Outcome { get; init; }

    /// <summary>Gets the number of payload bytes consumed.</summary>
    public long PayloadBytes { get; init; }

    /// <summary>Gets the previous target size before the write.</summary>
    public long PreviousBytes { get; init; }

    /// <summary>Gets the final committed target size.</summary>
    public long FinalBytes { get; init; }

    /// <summary>Gets the fingerprint of consumed payload bytes.</summary>
    public ContentHash PayloadFingerprint { get; init; }

    /// <summary>Gets the fingerprint of the committed final file.</summary>
    public ContentHash FinalFingerprint { get; init; }
}
