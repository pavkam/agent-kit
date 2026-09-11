// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolSchemaProfileIdTests: Conformance.StringIdentityConformanceTests<ToolSchemaProfileId>
{
    protected override ToolSchemaProfileId Create(string value) => new(value);
    protected override string? GetValue(ToolSchemaProfileId subject) => subject.Value;
}
