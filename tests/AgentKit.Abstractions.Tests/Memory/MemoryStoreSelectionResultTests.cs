// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryStoreSelectionResult"/> factories.</summary>
public sealed class MemoryStoreSelectionResultTests
{
    [Fact]
    public void Selected_WhenStoreIsSupplied_CarriesItWithoutAMessage()
    {
        var store = new StubStore();

        var result = MemoryStoreSelectionResult.Selected(store);

        result.IsSelected.ShouldBeTrue();
        result.Store.ShouldBeSameAs(store);
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void Selected_WhenStoreIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryStoreSelectionResult.Selected(null!)).ParamName.ShouldBe("store");

    [Fact]
    public void Rejected_WhenMessageIsSupplied_CarriesItWithoutAStore()
    {
        var result = MemoryStoreSelectionResult.Rejected("unknown key");

        result.IsSelected.ShouldBeFalse();
        result.Store.ShouldBeNull();
        result.SafeMessage.ShouldBe("unknown key");
    }

    [Fact]
    public void Rejected_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => MemoryStoreSelectionResult.Rejected(" ")).ParamName.ShouldBe("safeMessage");

    private sealed class StubStore: IMemoryStore
    {
        public MemoryStoreDescriptor Descriptor { get; } = new(new MemoryStoreKey("k"), "stub", new ComponentId("a"), false);

        public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
