// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="RetrievalRequestId"/> behavior and contracts.</summary>
public sealed class RetrievalRequestIdTests: GuidIdentityConformanceTests<RetrievalRequestId>
{
    /// <inheritdoc/>
    protected override RetrievalRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(RetrievalRequestId subject) => subject.Value;
}
