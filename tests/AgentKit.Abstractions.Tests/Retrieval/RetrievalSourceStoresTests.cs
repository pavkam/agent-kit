// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalSourceStores"/> constraints.</summary>
public sealed class RetrievalSourceStoresTests
{
    [Fact]
    public void Constructor_WhenNothingIsCaptured_HasNoStores()
    {
        var stores = new RetrievalSourceStores(null, null, default);

        stores.MemoryStore.ShouldBeNull();
        stores.DocumentStore.ShouldBeNull();
        stores.VectorIndexes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenAnIndexIsNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalSourceStores(null, null, [null!])).ParamName.ShouldBe("vectorIndexes");
}
