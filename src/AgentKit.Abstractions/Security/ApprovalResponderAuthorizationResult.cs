// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a closed responder-authorization outcome.</summary>
public abstract record ApprovalResponderAuthorizationResult;

/// <summary>Confirms that the authenticated responder may resolve the exact request.</summary>
public sealed record ApprovalResponderAuthorized: ApprovalResponderAuthorizationResult;

/// <summary>Rejects responder authority with a bounded non-sensitive reason.</summary>
/// <param name="SafeReason">The safe rejection reason.</param>
public sealed record ApprovalResponderUnauthorized(string SafeReason): ApprovalResponderAuthorizationResult
{
    /// <summary>Gets the bounded non-sensitive reason.</summary>
    public string SafeReason { get; } = !string.IsNullOrWhiteSpace(SafeReason)
        ? SafeReason
        : throw new ArgumentException("The reason cannot be blank.", nameof(SafeReason));
}
