// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableRecordResult behavior and contracts.</summary>
public sealed class DurableRecordResultTests
{
    [Fact]
    public void Hierarchy_ContainsOnlyTheThreeDeclaredKinds()
    {
        var kinds = typeof(DurableRecordResult).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(DurableRecordResult))).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe([nameof(DurableRecordFailed), nameof(DurableRecordFenced), nameof(DurableRecorded),]);
    }
}
