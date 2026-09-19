// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ApprovalChannelId behavior and contracts.</summary>
public sealed class ApprovalChannelIdTests: Conformance.StringIdentityConformanceTests<ApprovalChannelId>
{
    /// <inheritdoc/>
    protected override ApprovalChannelId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ApprovalChannelId subject) => subject.Value;
}
