// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports bounded stdout/stderr tails and truthful process-side-effect settlement.</summary>
public sealed record ProcessRunResult
{
    /// <summary>Initializes one terminal process result.</summary>
    /// <param name="status">The terminal process status.</param>
    /// <param name="exitCode">The observed exit code only for an exited process.</param>
    /// <param name="standardOutputTail">The retained stdout tail bytes.</param>
    /// <param name="standardErrorTail">The retained stderr tail bytes.</param>
    /// <param name="totalStandardOutputBytes">The non-negative observed stdout byte count.</param>
    /// <param name="totalStandardErrorBytes">The non-negative observed stderr byte count.</param>
    /// <param name="standardOutputTruncated">Whether earlier stdout bytes were discarded.</param>
    /// <param name="standardErrorTruncated">Whether earlier stderr bytes were discarded.</param>
    /// <param name="effectCertainty">What the host can prove about possible side effects.</param>
    /// <param name="safeMessage">A non-sensitive explanation or warning.</param>
    /// <param name="standardOutputArtifact">The complete stdout artifact when the retained tail truncated.</param>
    /// <param name="standardErrorArtifact">The complete stderr artifact when the retained tail truncated.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum or byte count is invalid.</exception>
    /// <exception cref="ArgumentException">An immutable byte array is default or the exit-code shape is inconsistent.</exception>
    public ProcessRunResult(
        ProcessRunStatus status,
        int? exitCode,
        ImmutableArray<byte> standardOutputTail,
        ImmutableArray<byte> standardErrorTail,
        long totalStandardOutputBytes,
        long totalStandardErrorBytes,
        bool standardOutputTruncated,
        bool standardErrorTruncated,
        ProcessSideEffectCertainty effectCertainty,
        string? safeMessage,
        ArtifactReference? standardOutputArtifact = null,
        ArtifactReference? standardErrorArtifact = null)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(standardOutputTail);
        ArgumentException.ThrowIfDefault(standardErrorTail);
        ArgumentOutOfRangeException.ThrowIfNegative(totalStandardOutputBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(totalStandardErrorBytes);
        ArgumentOutOfRangeException.ThrowIfUndefined(effectCertainty);
        if (status == ProcessRunStatus.Exited != exitCode.HasValue)
        {
            throw new ArgumentException("Only an exited process has an exit code.", nameof(exitCode));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            standardOutputTail.Length, totalStandardOutputBytes, nameof(standardOutputTail));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            standardErrorTail.Length, totalStandardErrorBytes, nameof(standardErrorTail));

        Status = status;
        ExitCode = exitCode;
        StandardOutputTail = standardOutputTail;
        StandardErrorTail = standardErrorTail;
        TotalStandardOutputBytes = totalStandardOutputBytes;
        TotalStandardErrorBytes = totalStandardErrorBytes;
        StandardOutputTruncated = standardOutputTruncated;
        StandardErrorTruncated = standardErrorTruncated;
        EffectCertainty = effectCertainty;
        SafeMessage = safeMessage;
        StandardOutputArtifact = standardOutputArtifact;
        StandardErrorArtifact = standardErrorArtifact;
    }

    /// <summary>Gets the terminal process status.</summary>
    public ProcessRunStatus Status { get; }
    /// <summary>Gets the observed process exit code.</summary>
    public int? ExitCode { get; }
    /// <summary>Gets the retained stdout tail bytes.</summary>
    public ImmutableArray<byte> StandardOutputTail { get; }
    /// <summary>Gets the retained stderr tail bytes.</summary>
    public ImmutableArray<byte> StandardErrorTail { get; }
    /// <summary>Gets the total observed stdout bytes.</summary>
    public long TotalStandardOutputBytes { get; }
    /// <summary>Gets the total observed stderr bytes.</summary>
    public long TotalStandardErrorBytes { get; }
    /// <summary>Gets whether earlier stdout bytes were discarded.</summary>
    public bool StandardOutputTruncated { get; }
    /// <summary>Gets whether earlier stderr bytes were discarded.</summary>
    public bool StandardErrorTruncated { get; }
    /// <summary>Gets what the host can prove about possible side effects.</summary>
    public ProcessSideEffectCertainty EffectCertainty { get; }
    /// <summary>Gets a non-sensitive explanation or warning.</summary>
    public string? SafeMessage { get; }
    /// <summary>Gets the complete stdout artifact when the retained tail truncated and preservation succeeded.</summary>
    public ArtifactReference? StandardOutputArtifact { get; }
    /// <summary>Gets the complete stderr artifact when the retained tail truncated and preservation succeeded.</summary>
    public ArtifactReference? StandardErrorArtifact { get; }

    /// <inheritdoc/>
    public bool Equals(ProcessRunResult? other) =>
        other is not null
        && Status == other.Status
        && ExitCode == other.ExitCode
        && StandardOutputTail.SequenceEqual(other.StandardOutputTail)
        && StandardErrorTail.SequenceEqual(other.StandardErrorTail)
        && TotalStandardOutputBytes == other.TotalStandardOutputBytes
        && TotalStandardErrorBytes == other.TotalStandardErrorBytes
        && StandardOutputTruncated == other.StandardOutputTruncated
        && StandardErrorTruncated == other.StandardErrorTruncated
        && EffectCertainty == other.EffectCertainty
        && SafeMessage == other.SafeMessage
        && StandardOutputArtifact == other.StandardOutputArtifact
        && StandardErrorArtifact == other.StandardErrorArtifact;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        hash.Add(ExitCode);
        foreach (var value in StandardOutputTail)
        {
            hash.Add(value);
        }

        foreach (var value in StandardErrorTail)
        {
            hash.Add(value);
        }

        hash.Add(TotalStandardOutputBytes);
        hash.Add(TotalStandardErrorBytes);
        hash.Add(StandardOutputTruncated);
        hash.Add(StandardErrorTruncated);
        hash.Add(EffectCertainty);
        hash.Add(SafeMessage);
        hash.Add(StandardOutputArtifact);
        hash.Add(StandardErrorArtifact);
        return hash.ToHashCode();
    }
}
