// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ProviderIdTests: Conformance.StringIdentityConformanceTests<ProviderId>
{

    /// <inheritdoc/>
    protected override ProviderId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ProviderId subject) => subject.Value;
}
