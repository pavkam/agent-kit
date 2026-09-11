// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class TenantIdTests: Conformance.StringIdentityConformanceTests<TenantId>
{

    /// <inheritdoc/>
    protected override TenantId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(TenantId subject) => subject.Value;
}
