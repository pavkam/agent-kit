// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ApprovalRequestIdTests: Conformance.GuidIdentityConformanceTests<ApprovalRequestId>
{

    /// <inheritdoc/>
    protected override ApprovalRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ApprovalRequestId subject) => subject.Value;
}
