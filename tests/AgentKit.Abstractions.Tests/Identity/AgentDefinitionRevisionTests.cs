// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AgentDefinitionRevisionTests: Conformance.LongIdentityConformanceTests<AgentDefinitionRevision>
{

    /// <inheritdoc/>
    protected override AgentDefinitionRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(AgentDefinitionRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
