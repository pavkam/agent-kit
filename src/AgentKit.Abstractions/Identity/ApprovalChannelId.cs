// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one trusted transport, such as a CLI, desktop UI, web application, or remote control plane, through which an approval response can arrive.</summary>
/// <remarks>This immutable value uses ordinal text equality. Naming a channel never authenticates a responder or grants authority; see <see cref="ApprovalAuthenticationEvidence"/> for the bound authentication evidence a response must still carry.</remarks>
public readonly record struct ApprovalChannelId
{
    /// <summary>Initializes a validated approval-channel identifier.</summary>
    /// <param name="value">The non-blank canonical channel identifier text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public ApprovalChannelId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical channel identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical channel identifier text.</summary>
    public override string ToString() => Value;
}
