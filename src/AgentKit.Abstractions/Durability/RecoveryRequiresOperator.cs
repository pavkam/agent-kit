// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The outcome is unknown, the effect is not safely repeatable, and no
/// automated reconciliation exists, so a human must resolve it.
/// </summary>
/// <remarks>
/// <para>
/// This decision exists so that the framework can stop honestly. A started
/// non-idempotent operation whose outcome cannot be determined has no correct
/// automatic answer: retrying may duplicate a real-world effect and skipping
/// may lose one.
/// </para>
/// <para>
/// Escalation is a legitimate terminal state, not a failure of the design.
/// Silently choosing one of the two unsafe options to avoid it would be the
/// actual failure.
/// </para>
/// </remarks>
public sealed record RecoveryRequiresOperator: RecoveryDecision
{
    private readonly string _safeReason;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryRequiresOperator"/> record.
    /// </summary>
    /// <param name="safeReason">
    /// A redacted, human-readable explanation of what is uncertain and what
    /// an operator must determine. It must not contain protected content.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeReason"/> is null, empty, or consists only of
    /// whitespace. An escalation without a reason is not actionable.
    /// </exception>
    public RecoveryRequiresOperator(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        _safeReason = safeReason;
    }

    /// <summary>
    /// Gets the redacted explanation of what an operator must determine.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeReason
    {
        get => _safeReason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeReason));
            _safeReason = value;
        }
    }
}
