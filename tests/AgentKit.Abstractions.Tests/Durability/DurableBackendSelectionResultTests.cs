// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableBackendSelectionResult"/> hierarchy closure.</summary>
public sealed class DurableBackendSelectionResultTests
{
    [Fact]
    public void Hierarchy_WhenInspected_IsAbstract() => typeof(DurableBackendSelectionResult).IsAbstract.ShouldBeTrue();

    [Fact]
    public void Hierarchy_WhenInspected_ContainsOnlySelectedAndRejected()
    {
        var kinds = typeof(DurableBackendSelectionResult).Assembly
            .GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(DurableBackendSelectionResult)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

        kinds.ShouldBe([nameof(DurableBackendSelected), nameof(DurableBackendSelectionRejected)]);
    }
}
