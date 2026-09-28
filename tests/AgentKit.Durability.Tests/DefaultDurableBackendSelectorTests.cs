// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Verifies exact, non-falling-back backend selection from an operation's captured context.</summary>
public sealed class DefaultDurableBackendSelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenTheOperationIsNull_ThrowsArgumentNullException()
    {
        var selector = new DefaultDurableBackendSelector(new StubBackendCatalog([]));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await selector.SelectAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("operation");
    }

    [Fact]
    public async Task SelectAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var selector = new DefaultDurableBackendSelector(new StubBackendCatalog([Descriptor("backend")]));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await selector.SelectAsync(DurableJournalTestData.Descriptor(), cancellation.Token));
    }

    [Fact]
    public async Task SelectAsync_WhenTheCapturedKeyIsRegistered_SelectsThatDescriptor()
    {
        var selector = new DefaultDurableBackendSelector(
            new StubBackendCatalog([Descriptor("other"), Descriptor("backend")]));

        var result = await selector.SelectAsync(
            DurableJournalTestData.Descriptor(),
            TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<DurableBackendSelected>();
        selected.Descriptor.Key.ShouldBe(new DurableBackendKey("backend"));
    }

    [Fact]
    public async Task SelectAsync_WhenTheCapturedKeyIsNotRegistered_RejectsInsteadOfFallingBack()
    {
        // Routing a resumed operation to a different backend would resume it against state it never wrote.
        var selector = new DefaultDurableBackendSelector(new StubBackendCatalog([Descriptor("other")]));

        var result = await selector.SelectAsync(
            DurableJournalTestData.Descriptor(),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableBackendSelectionRejected>();
    }

    [Fact]
    public async Task SelectAsync_WhenTheCatalogIsEmpty_Rejects()
    {
        var selector = new DefaultDurableBackendSelector(new StubBackendCatalog([]));

        var result = await selector.SelectAsync(
            DurableJournalTestData.Descriptor(),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableBackendSelectionRejected>();
    }

    private static DurableBackendDescriptor Descriptor(string key) =>
        new InMemoryDurableExecutionBackend(new DurableBackendKey(key)).Descriptor;

    /// <summary>A catalog returning exactly the descriptors the test supplied.</summary>
    private sealed class StubBackendCatalog(ImmutableArray<DurableBackendDescriptor> descriptors): IDurableBackendCatalog
    {
        public ImmutableArray<DurableBackendDescriptor> GetDescriptors() => descriptors;
    }
}
