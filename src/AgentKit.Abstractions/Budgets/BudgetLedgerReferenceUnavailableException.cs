// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals that an exact ledger reference cannot be used without revealing whether it is missing or foreign.</summary>
/// <remarks>No transition occurred. Adapters use this same safe failure for a missing row and an address mismatch so callers cannot probe another tenant's existence.</remarks>
public sealed class BudgetLedgerReferenceUnavailableException: InvalidOperationException
{
    /// <summary>Initializes a safe reference-unavailable failure.</summary>
    /// <param name="safeMessage">A nonblank content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public BudgetLedgerReferenceUnavailableException(string safeMessage) : base(safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }
    /// <summary>Gets the content-free explanation.</summary>
    /// <value>A nonblank message that does not disclose row existence or protected address details.</value>
    public string SafeMessage { get; }
}
