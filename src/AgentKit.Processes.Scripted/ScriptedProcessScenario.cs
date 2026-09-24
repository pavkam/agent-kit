// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Declares the terminal behavior of one identified process operation without creating a host process.</summary>
public sealed record ScriptedProcessScenario
{
    /// <summary>Initializes one deterministic process scenario.</summary>
    /// <param name="operationId">The exact operation identity selecting the scenario.</param>
    /// <param name="exit">The declared terminal exit outcome.</param>
    /// <param name="standardOutput">The complete stdout bytes emitted by the scenario.</param>
    /// <param name="standardError">The complete stderr bytes emitted by the scenario.</param>
    /// <param name="delayAfterStart">The non-negative injected-time delay after grant consumption.</param>
    /// <param name="standardOutputTruncated">Whether a stdout truncation marker is emitted.</param>
    /// <param name="standardErrorTruncated">Whether a stderr truncation marker is emitted.</param>
    /// <param name="totalStandardOutputBytes">The total stdout bytes reported by truncation; defaults to <paramref name="standardOutput"/> length.</param>
    /// <param name="totalStandardErrorBytes">The total stderr bytes reported by truncation; defaults to <paramref name="standardError"/> length.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exit"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity, delay, or byte total is invalid.</exception>
    /// <exception cref="ArgumentException">An immutable byte array is default.</exception>
    public ScriptedProcessScenario(
        ProcessOperationId operationId,
        ProcessExitResult exit,
        ImmutableArray<byte> standardOutput,
        ImmutableArray<byte> standardError,
        TimeSpan delayAfterStart,
        bool standardOutputTruncated = false,
        bool standardErrorTruncated = false,
        long? totalStandardOutputBytes = null,
        long? totalStandardErrorBytes = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId.Value, Guid.Empty, nameof(operationId));
        ArgumentNullException.ThrowIfNull(exit);
        if (standardOutput.IsDefault)
        {
            standardOutput = [];
        }

        if (standardError.IsDefault)
        {
            standardError = [];
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(delayAfterStart, TimeSpan.Zero);
        var stdoutTotal = totalStandardOutputBytes ?? standardOutput.Length;
        var stderrTotal = totalStandardErrorBytes ?? standardError.Length;
        ArgumentOutOfRangeException.ThrowIfNegative(stdoutTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(stderrTotal);
        ArgumentOutOfRangeException.ThrowIfLessThan(stdoutTotal, standardOutput.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(stderrTotal, standardError.Length);
        OperationId = operationId;
        Exit = exit;
        StandardOutput = standardOutput;
        StandardError = standardError;
        DelayAfterStart = delayAfterStart;
        StandardOutputTruncated = standardOutputTruncated;
        StandardErrorTruncated = standardErrorTruncated;
        TotalStandardOutputBytes = stdoutTotal;
        TotalStandardErrorBytes = stderrTotal;
    }

    /// <summary>Gets the exact operation identity selecting this scenario.</summary>
    public ProcessOperationId OperationId { get; }

    /// <summary>Gets the declared terminal exit outcome.</summary>
    public ProcessExitResult Exit { get; }

    /// <summary>Gets the complete stdout bytes emitted by the scenario.</summary>
    public ImmutableArray<byte> StandardOutput { get; }

    /// <summary>Gets the complete stderr bytes emitted by the scenario.</summary>
    public ImmutableArray<byte> StandardError { get; }

    /// <summary>Gets the injected-time delay after grant consumption.</summary>
    public TimeSpan DelayAfterStart { get; }

    /// <summary>Gets whether a stdout truncation marker is emitted.</summary>
    public bool StandardOutputTruncated { get; }

    /// <summary>Gets whether a stderr truncation marker is emitted.</summary>
    public bool StandardErrorTruncated { get; }

    /// <summary>Gets the total stdout bytes reported when truncation is declared.</summary>
    public long TotalStandardOutputBytes { get; }

    /// <summary>Gets the total stderr bytes reported when truncation is declared.</summary>
    public long TotalStandardErrorBytes { get; }
}
