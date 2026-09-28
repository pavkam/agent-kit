// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

using System.Collections.Immutable;

/// <summary>Verifies the process-local backend's honest capability claims and fail-closed refusals.</summary>
public sealed class InMemoryDurableExecutionBackendTests
{
    [Fact]
    public void Constructor_WhenTheKeyCarriesNoText_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new InMemoryDurableExecutionBackend(default));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenASupportedOperationIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new InMemoryDurableExecutionBackend(
                new DurableBackendKey("local"),
                [new DurableOperationName("tool.call"), default]));

        exception.ParamName.ShouldBe("supportedOperations");
    }

    [Fact]
    public void Constructor_WhenASupportedOperationIsDuplicated_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new InMemoryDurableExecutionBackend(
                new DurableBackendKey("local"),
                [new DurableOperationName("tool.call"), new DurableOperationName("tool.call")]));

        exception.ParamName.ShouldBe("supportedOperations");
    }

    [Fact]
    public void Constructor_WhenSupportedOperationsAreOmitted_PlacesNoRestriction()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        backend.Descriptor.SupportedOperations.ShouldBeEmpty();
    }

    [Fact]
    public void Descriptor_WhenConstructed_ClaimsOnlyProcessLocalCapabilities()
    {
        // A process-local owner has no second system that could hold an effect it cannot see.
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        var descriptor = backend.Descriptor;
        descriptor.Key.ShouldBe(new DurableBackendKey("local"));
        descriptor.Capabilities.SupportsDistributedOwnership.ShouldBeFalse();
        descriptor.Capabilities.SupportsExternalHandoff.ShouldBeFalse();
        descriptor.Capabilities.SupportsReconciliation.ShouldBeFalse();
        descriptor.SupportsReconciliation.ShouldBeFalse();
    }

    [Fact]
    public void Descriptor_WhenConstructed_ClaimsFencingBecauseTheLocalLeaseManagerIssuesTokens()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        backend.Descriptor.SupportsFencing.ShouldBeTrue();
    }

    [Fact]
    public void Descriptor_WhenSupportedOperationsAreDeclared_PublishesThemExactly()
    {
        var operations = ImmutableArray.Create(
            new DurableOperationName("tool.call"),
            new DurableOperationName("model.request"));

        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"), operations);

        backend.Descriptor.SupportedOperations.ShouldBe(operations);
    }

    [Fact]
    public async Task DispatchAsync_WhenTheRequestIsNull_ThrowsArgumentNullException()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await backend.DispatchAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task DispatchAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await backend.DispatchAsync(DispatchRequest(), cancellation.Token));
    }

    [Fact]
    public async Task DispatchAsync_WhenCalledDespiteTheCapabilityClaim_RefusesInsteadOfInventingAReference()
    {
        // A fabricated external reference would later route recovery into a reconciliation nobody can answer.
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        var result = await backend.DispatchAsync(DispatchRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableDispatchFailed>();
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheRequestIsNull_ThrowsArgumentNullException()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await backend.ReconcileAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ReconcileAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await backend.ReconcileAsync(ReconciliationRequest(), cancellation.Token));
    }

    [Fact]
    public async Task ReconcileAsync_WhenAnEffectIsUnknown_RefusesSoItIsNotTreatedAsProvenAbsent()
    {
        var backend = new InMemoryDurableExecutionBackend(new DurableBackendKey("local"));

        var result = await backend.ReconcileAsync(ReconciliationRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableReconciliationFailed>();
    }

    private static DurableDispatchRequest DispatchRequest() =>
        new(DurableJournalTestData.Descriptor(), DurableJournalTestData.Context());

    private static DurableReconciliationRequest ReconciliationRequest() =>
        new(
            DurableJournalTestData.Descriptor(),
            new RecoveryEvidence(
                DurableJournalTestData.Address(),
                DurableJournalTestData.Context(),
                DurableOperationState.EffectPending,
                SideEffectCertainty.Unknown,
                startDefinitelyAbsent: false,
                terminalResultRecorded: false));
}
