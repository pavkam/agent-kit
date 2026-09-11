// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SecurityRevocationVersionTests: Conformance.LongIdentityConformanceTests<SecurityRevocationVersion>
{

    /// <inheritdoc/>
    protected override SecurityRevocationVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(SecurityRevocationVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
