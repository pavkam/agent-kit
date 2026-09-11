// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class PlanRevisionTests: Conformance.LongIdentityConformanceTests<PlanRevision>
{

    /// <inheritdoc/>
    protected override PlanRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(PlanRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
