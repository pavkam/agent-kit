// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Supplies deterministic bootstrap evidence for durable security-store conformance and unit tests.</summary>
public static class SecurityControlPlaneTestBootstrap
{
    /// <summary>The stable capability identifier used by test hosts when initializing control-plane stores.</summary>
    public const string TestCapabilityId = "agentkit.test.security-control-plane";

    /// <summary>Creates bootstrap evidence stamped with the supplied or system clock.</summary>
    /// <param name="timeProvider">The clock used for <see cref="SecurityControlPlaneBootstrap.IssuedAt"/>; when null, <see cref="TimeProvider.System"/> is used.</param>
    /// <returns>Non-null bootstrap evidence suitable for store initialization overloads.</returns>
    public static SecurityControlPlaneBootstrap Create(TimeProvider? timeProvider = null) =>
        new(TestCapabilityId, (timeProvider ?? TimeProvider.System).GetUtcNow());
}
