// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The typed refusal a memory hook returns to stop a proposal or write.</summary>
/// <remarks>
/// A veto can only withhold: it can never accept a proposal that policy denied, retain content policy did not allow, or
/// widen visibility. The code and reason are surfaced to the caller and recorded as the denial, so neither may contain
/// memory content, credentials, or other secrets.
/// </remarks>
public sealed record MemoryHookVeto
{
    /// <summary>Initializes a veto.</summary>
    /// <param name="code">A short stable machine-readable denial code.</param>
    /// <param name="safeReason">A bounded human-readable reason that contains no memory content or secret.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public MemoryHookVeto(string code, string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Code = code;
        SafeReason = safeReason;
    }

    /// <summary>Gets the short stable machine-readable denial code.</summary>
    public string Code { get; }

    /// <summary>Gets the bounded human-readable reason, free of memory content and secrets.</summary>
    public string SafeReason { get; }
}
