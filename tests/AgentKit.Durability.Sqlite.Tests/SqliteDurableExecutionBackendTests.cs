// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the host-local backend advertises only what it can honor and refuses the rest.</summary>
public sealed class SqliteDurableExecutionBackendTests
{
    private static readonly DurableBackendKey Key = new("sqlite-backend");

    /// <summary>Verifies a keyless backend is refused, because a profile selects a backend by exact key.</summary>
    [Fact]
    public void Constructor_WhenKeyCarriesNoKeyText_ThrowsForTheKeyArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableExecutionBackend(default));

        exception.ParamName.ShouldBe("key");
    }

    /// <summary>Verifies the descriptor claims fencing but no distributed ownership, handoff, or reconciliation.</summary>
    [Fact]
    public void Descriptor_WhenCreated_ClaimsFencingWithoutDistributedOwnership()
    {
        var descriptor = new SqliteDurableExecutionBackend(Key).Descriptor;

        descriptor.Key.ShouldBe(Key);
        descriptor.SupportsFencing.ShouldBeTrue();
        descriptor.SupportsReconciliation.ShouldBeFalse();
        descriptor.Capabilities.SupportsDistributedOwnership.ShouldBeFalse();
        descriptor.Capabilities.SupportsExternalHandoff.ShouldBeFalse();
        descriptor.Capabilities.SupportsReconciliation.ShouldBeFalse();
    }

    /// <summary>Verifies an omitted operation list places no restriction rather than banning every operation.</summary>
    [Fact]
    public void Descriptor_WhenNoOperationsAreSupplied_PlacesNoOperationRestriction()
    {
        var descriptor = new SqliteDurableExecutionBackend(Key).Descriptor;

        descriptor.SupportedOperations.ShouldBeEmpty();
    }

    /// <summary>Verifies duplicate operation names are refused so selection cannot be ambiguous.</summary>
    [Fact]
    public void Constructor_WhenSupportedOperationsRepeatAName_ThrowsForThatArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableExecutionBackend(
            Key, [new DurableOperationName("call"), new DurableOperationName("call")]));

        exception.ParamName.ShouldBe("supportedOperations");
    }

    /// <summary>Verifies a nameless operation is refused rather than silently dropped from the claim.</summary>
    [Fact]
    public void Constructor_WhenASupportedOperationCarriesNoName_ThrowsForThatArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableExecutionBackend(
            Key, [new DurableOperationName("tool.call"), default]));

        exception.ParamName.ShouldBe("supportedOperations");
    }

    /// <summary>Verifies dispatch refuses rather than inventing an external handle recovery would trust.</summary>
    [Fact]
    public async Task DispatchAsync_WhenCalled_RefusesBecauseNoExternalHandoffIsAdvertised()
    {
        var backend = new SqliteDurableExecutionBackend(Key);

        var journalKey = new DurableJournalKey("sqlite-journal");
        var result = await backend.DispatchAsync(
            new DurableDispatchRequest(
                DurabilityConformanceData.Descriptor(journalKey),
                DurabilityConformanceData.Context(journalKey)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableDispatchFailed>();
    }

    /// <summary>Verifies reconciliation refuses, so an unknown effect escalates instead of being retried.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenCalled_RefusesBecauseNoAuthorityCanAnswer()
    {
        var backend = new SqliteDurableExecutionBackend(Key);

        var journalKey = new DurableJournalKey("sqlite-journal");
        var result = await backend.ReconcileAsync(
            new DurableReconciliationRequest(
                DurabilityConformanceData.Descriptor(journalKey),
                new RecoveryEvidence(
                    DurabilityConformanceData.Address(),
                    DurabilityConformanceData.Context(journalKey),
                    DurableOperationState.EffectPending,
                    SideEffectCertainty.Unknown,
                    startDefinitelyAbsent: false,
                    terminalResultRecorded: false,
                    externalReference: new ExternalOperationReference(Key, "handle"))),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableReconciliationFailed>();
    }

    /// <summary>Verifies a null dispatch request is refused before any declaration is inspected.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRequestIsNull_ThrowsForTheRequestArgument()
    {
        var backend = new SqliteDurableExecutionBackend(Key);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => backend.DispatchAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    /// <summary>Verifies a null reconciliation request is refused before any refusal is produced.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenRequestIsNull_ThrowsForTheRequestArgument()
    {
        var backend = new SqliteDurableExecutionBackend(Key);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => backend.ReconcileAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }
}
