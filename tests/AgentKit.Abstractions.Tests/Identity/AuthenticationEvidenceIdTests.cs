// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AuthenticationEvidenceIdTests: Conformance.StringIdentityConformanceTests<AuthenticationEvidenceId>
{

    /// <inheritdoc/>
    protected override AuthenticationEvidenceId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(AuthenticationEvidenceId subject) => subject.Value;
}
