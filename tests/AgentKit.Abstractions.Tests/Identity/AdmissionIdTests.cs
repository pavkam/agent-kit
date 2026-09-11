// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AdmissionIdTests: Conformance.GuidIdentityConformanceTests<AdmissionId>
{

    /// <inheritdoc/>
    protected override AdmissionId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(AdmissionId subject) => subject.Value;
}
