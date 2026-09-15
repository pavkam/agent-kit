// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a closed approval-broker outcome.</summary>
public abstract record ApprovalBrokerResult;

/// <summary>Returns the retained approved response.</summary>
/// <param name="Response">The exact terminal response retained by the store.</param>
public sealed record ApprovalBrokerApproved(ApprovalResponse Response): ApprovalBrokerResult
{
    /// <summary>Gets the retained response.</summary>
    public ApprovalResponse Response { get; } = Response ?? throw new ArgumentNullException(nameof(Response));
}

/// <summary>Returns the retained denied response.</summary>
/// <param name="Response">The exact terminal response retained by the store.</param>
public sealed record ApprovalBrokerDenied(ApprovalResponse Response): ApprovalBrokerResult
{
    /// <summary>Gets the retained response.</summary>
    public ApprovalResponse Response { get; } = Response ?? throw new ArgumentNullException(nameof(Response));
}

/// <summary>Reports that the request expired before a valid terminal resolution.</summary>
public sealed record ApprovalBrokerExpired: ApprovalBrokerResult;

/// <summary>Reports a fail-closed broker failure using bounded non-sensitive evidence.</summary>
/// <param name="SafeReason">The safe failure reason.</param>
public sealed record ApprovalBrokerUnavailable(string SafeReason): ApprovalBrokerResult
{
    /// <summary>Gets the bounded non-sensitive reason.</summary>
    public string SafeReason { get; } = !string.IsNullOrWhiteSpace(SafeReason)
        ? SafeReason
        : throw new ArgumentException("The reason cannot be blank.", nameof(SafeReason));
}
