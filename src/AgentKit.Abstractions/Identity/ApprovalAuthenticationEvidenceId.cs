// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one captured proof that a responder authenticated to a trusted approval channel before answering.</summary>
/// <remarks>This immutable value uses ordinal text equality. It names one piece of evidence for audit and replay; it does not itself authenticate anyone or grant authority.</remarks>
public readonly record struct ApprovalAuthenticationEvidenceId
{
    /// <summary>Initializes a validated authentication-evidence identifier.</summary>
    /// <param name="value">The non-blank canonical evidence identifier text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public ApprovalAuthenticationEvidenceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical evidence identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical evidence identifier text.</summary>
    public override string ToString() => Value;
}
