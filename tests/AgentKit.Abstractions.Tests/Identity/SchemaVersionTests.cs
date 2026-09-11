// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SchemaVersionTests: Conformance.StringIdentityConformanceTests<SchemaVersion>
{

    /// <inheritdoc/>
    protected override SchemaVersion Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(SchemaVersion subject) => subject.Value;
}
