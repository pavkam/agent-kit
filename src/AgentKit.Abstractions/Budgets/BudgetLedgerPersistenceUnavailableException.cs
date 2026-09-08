// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals that a ledger adapter could not confirm a read or transition outcome.</summary>
/// <remarks>
/// When <see cref="AcknowledgementUnknown"/> is true, a submitted mutation may
/// already be committed and callers must retry its exact immutable evidence.
/// When false, the adapter guarantees that no transition was committed. The
/// safe message is suitable for logs and callers; any causal inner exception
/// remains diagnostic evidence and must be handled under the host's redaction policy.
/// </remarks>
public sealed class BudgetLedgerPersistenceUnavailableException: InvalidOperationException
{
    /// <summary>Initializes a safe persistence-unavailable failure without a causal exception.</summary>
    /// <param name="safeMessage">A nonblank content-free explanation.</param>
    /// <param name="acknowledgementUnknown">True when a submitted mutation may have committed before the response failed; false when the adapter guarantees no transition committed.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public BudgetLedgerPersistenceUnavailableException(string safeMessage, bool acknowledgementUnknown)
        : base(safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
        AcknowledgementUnknown = acknowledgementUnknown;
    }

    /// <summary>Initializes a safe persistence-unavailable failure with causal diagnostic evidence.</summary>
    /// <param name="safeMessage">A nonblank content-free explanation safe for logs and callers.</param>
    /// <param name="acknowledgementUnknown">True when a submitted mutation may have committed before the response failed; false when the adapter guarantees no transition committed.</param>
    /// <param name="innerException">The non-null underlying adapter failure retained for diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="innerException"/> is null.</exception>
    public BudgetLedgerPersistenceUnavailableException(string safeMessage, bool acknowledgementUnknown, Exception innerException)
        : base(safeMessage, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(innerException);
        SafeMessage = safeMessage;
        AcknowledgementUnknown = acknowledgementUnknown;
    }
    /// <summary>Gets the content-free explanation.</summary><value>A nonblank safe message.</value>
    public string SafeMessage { get; }
    /// <summary>Gets whether a submitted mutation may have committed.</summary>
    /// <value>False guarantees no transition committed; true requires exact replay.</value>
    public bool AcknowledgementUnknown { get; }
}
