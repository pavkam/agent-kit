// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed outcome of dispatching one approval request.</summary>
public abstract record ApprovalHandlerResult;

/// <summary>Returns a candidate terminal response from a trusted approval channel.</summary>
/// <param name="Response">The candidate response, which still requires authorization and binding validation.</param>
public sealed record ApprovalHandlerResponded(ApprovalResponse Response): ApprovalHandlerResult
{
    /// <summary>Gets the non-null candidate response.</summary>
    public ApprovalResponse Response { get; } = Response ?? throw new ArgumentNullException(nameof(Response));
}

/// <summary>Reports that the configured channel cannot resolve the request inline.</summary>
/// <param name="SafeReason">The bounded non-sensitive reason.</param>
public sealed record ApprovalHandlerUnavailable(string SafeReason): ApprovalHandlerResult
{
    /// <summary>Gets the bounded non-sensitive reason.</summary>
    public string SafeReason { get; } = !string.IsNullOrWhiteSpace(SafeReason)
        ? SafeReason
        : throw new ArgumentException("The reason cannot be blank.", nameof(SafeReason));
}
