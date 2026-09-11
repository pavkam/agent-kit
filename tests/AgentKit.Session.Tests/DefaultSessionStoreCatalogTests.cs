// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;
/// <summary>Verifies DefaultSessionStoreCatalog behavior and contracts.</summary>
public sealed class DefaultSessionStoreCatalogTests
{
    [Fact]
    public void CatalogConstructor_WhenStoreDescriptorMutationIsInvalid_RejectsBeforeCapturingBinding()
    {
        var store = new FakeSessionStore();
        var descriptor = store.Descriptor;
        store.OnDescriptor = () => descriptor with
        {
            Key = default
        };
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultSessionStoreCatalog([store]));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
        store.DescriptorReadCount.ShouldBe(2);
    }

    [Fact]
    public void GetDescriptors_WhenStoresAreProvided_ReturnsAStableKeyOrderedSnapshot()
    {
        var store = new FakeSessionStore();
        var catalog = new DefaultSessionStoreCatalog([store]);
        catalog.GetDescriptors().ShouldBe([store.Descriptor]);
    }
}
