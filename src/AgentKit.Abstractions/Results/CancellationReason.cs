// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains normalized cancellation of accepted work, distinct from timeout or detached caller waiting.</summary>
/// <remarks>Constructing this value does not request durable abort. The operation owner establishes cancellation and records its safe error evidence.</remarks>
public sealed record CancellationReason
{
    /// <summary>Captures a normalized cancellation error.</summary>
    /// <param name="error">The nonnull error whose exact code is Cancelled.</param>
    /// <exception cref="ArgumentNullException">The error is null.</exception>
    /// <exception cref="ArgumentException">The error is not classified as cancellation.</exception>
    public CancellationReason(AgentError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNotEqual(error.Code, AgentErrorCodes.Cancelled, nameof(error));
        Error = error;
    }
    /// <summary>Gets the original safe cancellation evidence and correlation.</summary>
    /// <value>A nonnull error with the exact Cancelled code; timeout remains a separate error category.</value>
    public AgentError Error { get; }
}
