// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ApiFamilyIdTests: Conformance.StringIdentityConformanceTests<ApiFamilyId>
{

    /// <inheritdoc/>
    protected override ApiFamilyId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ApiFamilyId subject) => subject.Value;
}
