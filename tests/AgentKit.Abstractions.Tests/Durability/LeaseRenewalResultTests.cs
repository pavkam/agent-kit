// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies LeaseRenewalResult behavior and contracts.</summary>
public sealed class LeaseRenewalResultTests
{
    [Fact]
    public void LeaseRenewalResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(LeaseRenewalResult).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(LeaseRenewalResult))).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe([nameof(LeaseLost), nameof(LeaseRenewed)]);
    }
}
