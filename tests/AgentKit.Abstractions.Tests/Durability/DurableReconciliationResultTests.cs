// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableReconciliationResult"/> hierarchy closure.</summary>
public sealed class DurableReconciliationResultTests
{
    [Fact]
    public void Hierarchy_WhenInspected_IsAbstract() => typeof(DurableReconciliationResult).IsAbstract.ShouldBeTrue();

    [Fact]
    public void Hierarchy_WhenInspected_ContainsOnlyReconciledAndFailed()
    {
        var kinds = typeof(DurableReconciliationResult).Assembly
            .GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(DurableReconciliationResult)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

        kinds.ShouldBe([nameof(DurableReconciled), nameof(DurableReconciliationFailed)]);
    }
}
