// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SessionStoreKeyTests: Conformance.StringIdentityConformanceTests<SessionStoreKey>
{

    /// <inheritdoc/>
    protected override SessionStoreKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(SessionStoreKey subject) => subject.Value;
}
