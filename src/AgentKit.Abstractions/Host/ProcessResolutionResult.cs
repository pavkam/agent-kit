// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports canonical process resolution or a typed pre-authorization failure.</summary>
public sealed record ProcessResolutionResult
{
    /// <summary>Initializes a process-resolution result.</summary>
    /// <param name="status">The terminal resolution status.</param>
    /// <param name="intent">The resolved intent only when <paramref name="status"/> is resolved.</param>
    /// <param name="safeMessage">A non-sensitive failure explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The intent/status shape is inconsistent.</exception>
    public ProcessResolutionResult(
        ProcessResolutionStatus status,
        ResolvedProcessIntent? intent,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        if (status == ProcessResolutionStatus.Resolved != (intent is not null))
        {
            throw new ArgumentException("Only a resolved result may contain an intent.", nameof(intent));
        }

        if (status != ProcessResolutionStatus.Resolved)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        }

        Status = status;
        Intent = intent;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal resolution status.</summary>
    public ProcessResolutionStatus Status { get; }
    /// <summary>Gets the immutable resolved intent when successful.</summary>
    public ResolvedProcessIntent? Intent { get; }
    /// <summary>Gets the non-sensitive failure explanation.</summary>
    public string? SafeMessage { get; }
}
