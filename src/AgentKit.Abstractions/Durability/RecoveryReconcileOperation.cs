// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The outcome is unknown but the effect owner can be queried, so recovery
/// asks what actually happened before deciding anything else.
/// </summary>
/// <remarks>
/// Reconciliation is the correct answer to an unknown outcome whenever the
/// external system can be asked. It converts uncertainty into fact instead of
/// gambling on a retry, and it is the only safe path for a non-idempotent
/// effect that may already have occurred.
/// </remarks>
public sealed record RecoveryReconcileOperation: RecoveryDecision
{
    private readonly ExternalOperationReference _reference;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryReconcileOperation"/> record.
    /// </summary>
    /// <param name="reference">
    /// The external owner and handle to query for the true outcome.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reference"/> is <see langword="null"/>. Reconciliation
    /// is meaningless without an owner to ask.
    /// </exception>
    public RecoveryReconcileOperation(ExternalOperationReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _reference = reference;
    }

    /// <summary>Gets the external owner and handle to query.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ExternalOperationReference Reference
    {
        get => _reference;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Reference));
            _reference = value;
        }
    }
}
