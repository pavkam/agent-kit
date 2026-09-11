// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SessionEntryIdTests: Conformance.GuidIdentityConformanceTests<SessionEntryId>
{

    /// <inheritdoc/>
    protected override SessionEntryId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SessionEntryId subject) => subject.Value;
}
