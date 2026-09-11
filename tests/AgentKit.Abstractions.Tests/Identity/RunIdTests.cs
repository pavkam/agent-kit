// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class RunIdTests: Conformance.GuidIdentityConformanceTests<RunId>
{

    /// <inheritdoc/>
    protected override RunId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(RunId subject) => subject.Value;
}
