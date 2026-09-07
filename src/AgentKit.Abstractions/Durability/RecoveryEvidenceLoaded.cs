// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The journal returned complete durable evidence for the requested
/// operation.
/// </summary>
public sealed record RecoveryEvidenceLoaded: RecoveryEvidenceResult
{
    private readonly RecoveryEvidence _evidence;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryEvidenceLoaded"/> record.
    /// </summary>
    /// <param name="evidence">
    /// The assembled evidence describing what is durably known.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="evidence"/> is <see langword="null"/>.
    /// </exception>
    public RecoveryEvidenceLoaded(RecoveryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _evidence = evidence;
    }

    /// <summary>Gets the assembled durable evidence.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public RecoveryEvidence Evidence
    {
        get => _evidence;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Evidence));
            _evidence = value;
        }
    }
}
