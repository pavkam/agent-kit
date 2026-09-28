// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableExecutionEvent"/> hierarchy and shared base behavior.</summary>
public sealed class DurableExecutionEventTests
{
    [Fact]
    public void Hierarchy_WhenInspected_IsAbstract() => typeof(DurableExecutionEvent).IsAbstract.ShouldBeTrue();

    [Fact]
    public void Hierarchy_WhenInspected_ContainsOnlyTheFiveDeclaredObservations()
    {
        var kinds = typeof(DurableExecutionEvent).Assembly
            .GetTypes()
            .Where(static type => type.IsSubclassOf(typeof(DurableExecutionEvent)))
            .Select(static type => type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal);

        kinds.ShouldBe([
            nameof(DurableOperationAccepted),
            nameof(DurableOperationCheckpointed),
            nameof(DurableOperationDeferred),
            nameof(DurableOperationRecoveryPlanned),
            nameof(DurableOperationSettled),
        ]);
    }

    [Fact]
    public void Constructor_WhenBindingIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationSettled(
            null!,
            DurabilityTestData.Now,
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("binding");
    }

    [Fact]
    public void Constructor_WhenBindingIsSupplied_ExposesCausalCoordinates()
    {
        var binding = DurabilityTestData.Binding();

        DurableExecutionEvent observed = new DurableOperationSettled(
            binding,
            DurabilityTestData.Now,
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            DurabilityTestData.Token);

        observed.Binding.ShouldBe(binding);
        observed.OccurredAt.ShouldBe(DurabilityTestData.Now);
    }
}
