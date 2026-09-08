// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.Conformance;

/// <summary>Runs reusable audit-dispatcher behavioral conformance against the default Permissions composition.</summary>
public sealed class DefaultSecurityAuditDispatcherConformanceTests:
    SecurityAuditDispatcherConformanceTests<DefaultSecurityAuditDispatcherConformanceFixture>
{
    /// <summary>Creates isolated default dispatcher composition with a deterministic fixture clock.</summary>
    /// <returns>The fixture used by the inherited contract case.</returns>
    protected override DefaultSecurityAuditDispatcherConformanceFixture CreateFixture() => new();
}
