// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>Verifies SessionStoreBinding behavior and contracts.</summary>
public sealed class SessionStoreBindingTests
{
    [Fact]
    public void Constructor_WhenStoreOrDescriptorIsNull_ThrowsExactArgumentNullException()
    {
        var store = new FakeSessionStore();
        Should.Throw<ArgumentNullException>(() => new SessionStoreBinding(null!, store.Descriptor))
            .ParamName.ShouldBe("store");
        Should.Throw<ArgumentNullException>(() => new SessionStoreBinding(store, null!))
            .ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RetainsExactInstances()
    {
        var store = new FakeSessionStore();

        var binding = new SessionStoreBinding(store, store.Descriptor);

        binding.Store.ShouldBeSameAs(store);
        binding.Descriptor.ShouldBeSameAs(store.Descriptor);
    }

    [Fact]
    public void Equals_WhenBindingsShareStoreAndDescriptor_AreEqual()
    {
        var store = new FakeSessionStore();
        var first = new SessionStoreBinding(store, store.Descriptor);
        var second = new SessionStoreBinding(store, store.Descriptor);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenStoresDiffer_AreNotEqual()
    {
        var first = new FakeSessionStore();
        var second = new FakeSessionStore();

        new SessionStoreBinding(first, first.Descriptor).ShouldNotBe(new SessionStoreBinding(second, second.Descriptor));
    }

    [Fact]
    public void With_WhenCloned_ProducesAnEquivalentInstance()
    {
        var store = new FakeSessionStore();
        var original = new SessionStoreBinding(store, store.Descriptor);

        var clone = original with { };

        clone.ShouldBe(original);
    }
}
