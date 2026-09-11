// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class WebSearchRequestIdTests: Conformance.GuidIdentityConformanceTests<WebSearchRequestId>
{

    /// <inheritdoc/>
    protected override WebSearchRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(WebSearchRequestId subject) => subject.Value;
}
