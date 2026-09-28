// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableDispatchResult"/> hierarchy closure.</summary>
public sealed class DurableDispatchResultTests
{
    [Fact]
    public void Hierarchy_WhenInspected_IsAbstract() => typeof(DurableDispatchResult).IsAbstract.ShouldBeTrue();

    [Fact]
    public void Hierarchy_WhenInspected_ContainsOnlyDispatchedAndFailed()
    {
        var kinds = typeof(DurableDispatchResult).Assembly
            .GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(DurableDispatchResult)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

        kinds.ShouldBe([nameof(DurableDispatchFailed), nameof(DurableDispatched)]);
    }
}
