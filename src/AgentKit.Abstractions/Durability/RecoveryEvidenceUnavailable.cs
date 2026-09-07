// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal could not be read, so nothing is known about the requested
/// operation.
/// </summary>
/// <remarks>
/// This is emphatically not <see cref="RecoveryEvidenceNotFound"/>. A storage
/// outage means the evidence is unavailable, not absent, and recovery must
/// fail closed by waiting or escalating rather than concluding that work
/// never started and beginning it again.
/// </remarks>
public sealed record RecoveryEvidenceUnavailable: RecoveryEvidenceResult
{
    private readonly string _safeMessage;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryEvidenceUnavailable"/> record.
    /// </summary>
    /// <param name="safeMessage">
    /// A redacted, human-readable description of why the journal could not be
    /// read. It must not contain credentials, connection strings, or
    /// protected content.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public RecoveryEvidenceUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        _safeMessage = safeMessage;
    }

    /// <summary>
    /// Gets the redacted description of why the journal could not be read.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeMessage
    {
        get => _safeMessage;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeMessage));
            _safeMessage = value;
        }
    }
}
