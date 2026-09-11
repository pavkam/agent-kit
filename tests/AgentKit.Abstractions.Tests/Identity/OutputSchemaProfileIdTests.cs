// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class OutputSchemaProfileIdTests: Conformance.StringIdentityConformanceTests<OutputSchemaProfileId>
{

    /// <inheritdoc/>
    protected override OutputSchemaProfileId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(OutputSchemaProfileId subject) => subject.Value;
}
