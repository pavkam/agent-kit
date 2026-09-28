// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries observation content that survived redaction under the active policy.</summary>
public sealed record RedactedContent: RedactionResult
{
    /// <summary>Initializes a successful redaction result.</summary>
    /// <param name="content">The non-null redacted content safe for export.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public RedactedContent(ObservationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets the redacted content safe for export.</summary>
    public ObservationContent Content { get; }
}
