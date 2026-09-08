// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals that a known reservation cannot perform the requested transition in its persisted state.</summary>
/// <remarks>No transition occurred; for example, settlement before start and correction before settlement fail with this exception.</remarks>
public sealed class BudgetLedgerStateException: InvalidOperationException
{
    /// <summary>Initializes a safe no-transition state failure.</summary><param name="safeMessage">A nonblank content-free explanation.</param><exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public BudgetLedgerStateException(string safeMessage) : base(safeMessage) { ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage); SafeMessage = safeMessage; }
    /// <summary>Gets the content-free explanation.</summary><value>A nonblank safe message.</value>
    public string SafeMessage { get; }
}
