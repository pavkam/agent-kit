// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ToolIdTests: Conformance.StringIdentityConformanceTests<ToolId>
{

    /// <inheritdoc/>
    protected override ToolId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ToolId subject) => subject.Value;
}
