// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ToolCallIdTests: Conformance.GuidIdentityConformanceTests<ToolCallId>
{

    /// <inheritdoc/>
    protected override ToolCallId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ToolCallId subject) => subject.Value;
}
