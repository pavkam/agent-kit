// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one safe authentication-evidence record without containing the credential itself.</summary>
public readonly record struct AuthenticationEvidenceId
{
    /// <summary>Initializes an evidence identifier.</summary>
    /// <param name="value">The non-blank evidence reference.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public AuthenticationEvidenceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the safe evidence reference.</summary>
    public string Value { get; }

    /// <summary>Returns the safe evidence reference.</summary>
    public override string ToString() => Value;
}
