// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for terminal process exit outcomes.</summary>
public abstract record ProcessExitResult
{
    /// <summary>Initializes the base exit outcome.</summary>
    /// <param name="sideEffectCertainty">What the host can prove about effects.</param>
    private protected ProcessExitResult(SideEffectCertainty sideEffectCertainty)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets what the host can prove about effects.</summary>
    public SideEffectCertainty SideEffectCertainty { get; init; }
}

/// <summary>The process exited with an observed exit code.</summary>
public sealed record ProcessExited: ProcessExitResult
{
    /// <summary>Initializes an exited result.</summary>
    /// <param name="exitCode">The observed exit code.</param>
    /// <param name="sideEffectCertainty">What the host can prove about effects.</param>
    public ProcessExited(int exitCode, SideEffectCertainty sideEffectCertainty = SideEffectCertainty.DefinitelyPerformed)
        : base(sideEffectCertainty) => ExitCode = exitCode;

    /// <summary>Gets the observed exit code.</summary>
    public int ExitCode { get; init; }
}

/// <summary>The process was cancelled before or during execution.</summary>
/// <param name="SideEffectCertainty">What the host can prove about effects.</param>
public sealed record ProcessCancelled(SideEffectCertainty SideEffectCertainty): ProcessExitResult(SideEffectCertainty);

/// <summary>The process timed out according to policy.</summary>
/// <param name="SideEffectCertainty">What the host can prove about effects.</param>
public sealed record ProcessTimedOut(SideEffectCertainty SideEffectCertainty): ProcessExitResult(SideEffectCertainty);
