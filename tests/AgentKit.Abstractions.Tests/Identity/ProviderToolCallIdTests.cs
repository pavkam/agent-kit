// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ProviderToolCallIdTests: Conformance.StringIdentityConformanceTests<ProviderToolCallId>
{

    /// <inheritdoc/>
    protected override ProviderToolCallId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ProviderToolCallId subject) => subject.Value;
}
