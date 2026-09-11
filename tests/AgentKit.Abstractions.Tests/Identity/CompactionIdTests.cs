// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class CompactionIdTests: Conformance.GuidIdentityConformanceTests<CompactionId>
{

    /// <inheritdoc/>
    protected override CompactionId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(CompactionId subject) => subject.Value;
}
