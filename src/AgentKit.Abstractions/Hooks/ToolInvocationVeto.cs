// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A hook's typed refusal of one tool call before it is authorized or invoked.</summary>
/// <remarks>
/// A veto is a host-level refusal, not a security decision: it produces a rejected terminal result whose safe
/// reason the model sees, and it never grants, widens, or consumes authority. Denials by the security authority
/// remain distinct outcomes.
/// </remarks>
public sealed record ToolInvocationVeto
{
    /// <summary>Initializes a veto.</summary>
    /// <param name="safeReason">A content-safe, nonblank reason suitable for the model and for diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public ToolInvocationVeto(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public string SafeReason { get; }
}
