// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a prepared sandbox launch or typed fail-closed rejection.</summary>
public sealed record ProcessSandboxResult
{
    /// <summary>Initializes a sandbox preparation result.</summary>
    /// <param name="status">The terminal preparation status.</param>
    /// <param name="launch">The enforcing launch only for a ready result.</param>
    /// <param name="safeMessage">A non-sensitive failure explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The status and launch shape are inconsistent.</exception>
    public ProcessSandboxResult(
        ProcessSandboxStatus status,
        ProcessSandboxLaunch? launch,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        if (status == ProcessSandboxStatus.Ready != (launch is not null))
        {
            throw new ArgumentException("Only a ready sandbox result may contain a launch.", nameof(launch));
        }

        if (status != ProcessSandboxStatus.Ready)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        }

        Status = status;
        Launch = launch;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal preparation status.</summary>
    public ProcessSandboxStatus Status { get; }
    /// <summary>Gets the enforcing wrapper launch when ready.</summary>
    public ProcessSandboxLaunch? Launch { get; }
    /// <summary>Gets the non-sensitive failure explanation.</summary>
    public string? SafeMessage { get; }
}
