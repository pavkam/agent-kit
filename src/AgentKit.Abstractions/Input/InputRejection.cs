// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides a typed, content-free explanation for input rejected before an admission or promotion mutation.</summary>
/// <remarks>The reason classifies the failed boundary without disclosing caller payload, authorization material, or durable record content.</remarks>
public sealed record InputRejection
{
    /// <summary>Initializes a typed content-free rejection explanation.</summary>
    /// <param name="kind">The defined class of pre-mutation rejection.</param>
    /// <param name="safeReason">A non-null, non-whitespace explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public InputRejection(InputRejectionKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind; SafeReason = safeReason;
    }
    /// <summary>Gets the stable class of pre-mutation rejection.</summary>
    /// <value>A defined kind that callers can handle without parsing message text.</value>
    public InputRejectionKind Kind { get; }
    /// <summary>Gets the content-free explanation of the rejection.</summary>
    /// <value>A non-empty safe string that excludes caller input, security grants, and other protected content.</value>
    public string SafeReason { get; }
}
