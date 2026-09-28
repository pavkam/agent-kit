// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The outcome of one budget policy evaluation.</summary>
public abstract record BudgetPolicyDecision;

/// <summary>Allows the evaluated operation to proceed under the captured profile and request.</summary>
public sealed record BudgetPolicyAllowed: BudgetPolicyDecision;

/// <summary>Denies the evaluated operation with a content-safe explanation.</summary>
public sealed record BudgetPolicyDenied: BudgetPolicyDecision
{
    /// <summary>Initializes a denial decision.</summary>
    /// <param name="safeMessage">The content-safe denial reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public BudgetPolicyDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-safe denial reason.</summary>
    public string SafeMessage { get; }
}
