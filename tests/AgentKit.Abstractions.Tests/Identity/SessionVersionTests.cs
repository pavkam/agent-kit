// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SessionVersionTests: Conformance.LongIdentityConformanceTests<SessionVersion>
{

    /// <inheritdoc/>
    protected override SessionVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(SessionVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
