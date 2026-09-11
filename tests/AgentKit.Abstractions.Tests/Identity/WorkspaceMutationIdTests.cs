// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class WorkspaceMutationIdTests: Conformance.GuidIdentityConformanceTests<WorkspaceMutationId>
{

    /// <inheritdoc/>
    protected override WorkspaceMutationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(WorkspaceMutationId subject) => subject.Value;
}
