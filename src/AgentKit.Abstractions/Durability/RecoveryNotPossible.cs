// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The operation cannot be recovered by this composition, normally because
/// the recorded contract version or backend can no longer be interpreted.
/// </summary>
/// <remarks>
/// This decision is the explicit alternative to silently reinterpreting
/// recorded input under new code. When an operation's
/// <see cref="DurableOperationVersion"/> is unsupported, the correct outcomes
/// are an explicit migration, a pinned old version, or this typed stop —
/// never a hopeful decode.
/// </remarks>
public sealed record RecoveryNotPossible: RecoveryDecision
{
    private readonly string _safeReason;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryNotPossible"/>
    /// record.
    /// </summary>
    /// <param name="safeReason">
    /// A redacted, human-readable explanation of why recovery cannot
    /// proceed. It must not contain protected content.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeReason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public RecoveryNotPossible(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        _safeReason = safeReason;
    }

    /// <summary>
    /// Gets the redacted explanation of why recovery cannot proceed.
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
