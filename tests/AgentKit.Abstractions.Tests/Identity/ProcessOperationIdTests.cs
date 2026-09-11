// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ProcessOperationIdTests: Conformance.GuidIdentityConformanceTests<ProcessOperationId>
{

    /// <inheritdoc/>
    protected override ProcessOperationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ProcessOperationId subject) => subject.Value;
}
