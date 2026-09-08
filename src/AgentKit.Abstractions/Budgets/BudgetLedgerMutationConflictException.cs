// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals reuse of a persisted mutation identity with non-equivalent immutable evidence.</summary>
/// <remarks>No transition occurred. Callers must not retry altered evidence under the same reservation, revision, or idempotency identity.</remarks>
public sealed class BudgetLedgerMutationConflictException: InvalidOperationException
{
    /// <summary>Initializes a safe no-transition conflict.</summary><param name="safeMessage">A nonblank content-free explanation.</param><exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public BudgetLedgerMutationConflictException(string safeMessage) : base(safeMessage) { ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage); SafeMessage = safeMessage; }
    /// <summary>Gets the content-free explanation.</summary><value>A nonblank safe message.</value>
    public string SafeMessage { get; }
}
