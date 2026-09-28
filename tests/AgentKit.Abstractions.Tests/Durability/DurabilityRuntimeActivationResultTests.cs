// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurabilityRuntimeActivationResult"/> hierarchy closure.</summary>
public sealed class DurabilityRuntimeActivationResultTests
{
    [Fact]
    public void Hierarchy_WhenInspected_IsAbstract() => typeof(DurabilityRuntimeActivationResult).IsAbstract.ShouldBeTrue();

    [Fact]
    public void Hierarchy_WhenInspected_ContainsOnlyActivatedAndFailed()
    {
        var kinds = typeof(DurabilityRuntimeActivationResult).Assembly
            .GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(DurabilityRuntimeActivationResult)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

        kinds.ShouldBe([nameof(DurabilityRuntimeActivated), nameof(DurabilityRuntimeActivationFailed)]);
    }
}
