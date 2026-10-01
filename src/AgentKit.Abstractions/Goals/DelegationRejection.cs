// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Explains a delegation that was refused before any child identity existed.</summary>
/// <remarks>The message is content-safe by contract: it never echoes prompts, tool arguments, or credentials.</remarks>
public sealed record DelegationRejection
{
    /// <summary>Initializes a validated rejection.</summary>
    /// <param name="kind">The defined rejection class.</param>
    /// <param name="safeMessage">The non-blank content-safe explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public DelegationRejection(DelegationRejectionKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the rejection class.</summary>
    public DelegationRejectionKind Kind { get; }

    /// <summary>Gets the content-safe explanation.</summary>
    public string SafeMessage { get; }
}
