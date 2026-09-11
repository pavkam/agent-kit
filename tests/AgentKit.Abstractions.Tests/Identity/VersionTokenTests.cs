// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class VersionTokenTests: Conformance.StringIdentityConformanceTests<VersionToken>
{

    /// <inheritdoc/>
    protected override VersionToken Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(VersionToken subject) => subject.Value;
}
