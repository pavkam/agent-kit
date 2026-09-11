// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AgentDefinitionSourceIdTests: Conformance.StringIdentityConformanceTests<AgentDefinitionSourceId>
{

    /// <inheritdoc/>
    protected override AgentDefinitionSourceId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(AgentDefinitionSourceId subject) => subject.Value;
}
