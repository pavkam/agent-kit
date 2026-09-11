// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ExternalRequestIdTests: Conformance.StringIdentityConformanceTests<ExternalRequestId>
{

    /// <inheritdoc/>
    protected override ExternalRequestId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ExternalRequestId subject) => subject.Value;
}
