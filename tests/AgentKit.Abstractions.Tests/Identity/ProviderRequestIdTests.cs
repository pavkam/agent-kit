// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ProviderRequestIdTests: Conformance.StringIdentityConformanceTests<ProviderRequestId>
{

    /// <inheritdoc/>
    protected override ProviderRequestId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ProviderRequestId subject) => subject.Value;
}
