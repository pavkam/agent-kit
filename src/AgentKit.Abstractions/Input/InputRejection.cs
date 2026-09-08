// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides a typed, content-free explanation for rejected input.</summary>
public sealed record InputRejection
{
    /// <summary>Initializes a rejection.</summary>
    /// <param name="kind">The stable rejection class.</param><param name="safeReason">A nonempty reason safe for callers and diagnostics.</param>
    public InputRejection(InputRejectionKind kind, string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind; SafeReason = safeReason;
    }
    /// <summary>Gets rejection class.</summary><value>The stable kind.</value>
    public InputRejectionKind Kind { get; }
    /// <summary>Gets safe reason.</summary><value>Content-free explanatory text.</value>
    public string SafeReason { get; }
}
