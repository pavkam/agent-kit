// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a durable approval request was retained without an inline terminal response.</summary>
/// <param name="Request">The exact request persisted for later resolution.</param>
public sealed record ApprovalBrokerDeferred(ApprovalRequest Request): ApprovalBrokerResult
{
    /// <summary>Gets the retained durable request.</summary>
    public ApprovalRequest Request { get; } = Request ?? throw new ArgumentNullException(nameof(Request));
}
