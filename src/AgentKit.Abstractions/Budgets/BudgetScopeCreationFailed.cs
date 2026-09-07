// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Scope creation failed before any scope was created.</summary>
public sealed record BudgetScopeCreationFailed: BudgetScopeResult
{
    /// <summary>Initializes a new instance of the <see cref="BudgetScopeCreationFailed"/> record.</summary>
    /// <param name="kind">The category of this failure.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public BudgetScopeCreationFailed(BudgetScopeCreationFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the category of this failure.</summary>
    public BudgetScopeCreationFailureKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
