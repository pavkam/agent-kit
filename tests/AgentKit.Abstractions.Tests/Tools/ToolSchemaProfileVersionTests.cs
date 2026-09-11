// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolSchemaProfileVersionTests: Conformance.LongIdentityConformanceTests<ToolSchemaProfileVersion>
{
    protected override ToolSchemaProfileVersion Create(long value) => new(value);
    protected override long GetValue(ToolSchemaProfileVersion subject) => subject.Value;
    protected override bool RequiresPositiveValue => true;
}
