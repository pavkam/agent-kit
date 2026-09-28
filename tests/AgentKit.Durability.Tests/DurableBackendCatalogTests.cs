// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies that the backend catalog snapshots every keyed registration's descriptor.</summary>
public sealed class DurableBackendCatalogTests
{
    [Fact]
    public void Constructor_WhenTheContainerIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableBackendCatalog(null!));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void GetDescriptors_WhenNoBackendIsRegistered_ReturnsAnEmptyCatalog()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var catalog = new DurableBackendCatalog(provider);

        catalog.GetDescriptors().ShouldBeEmpty();
    }

    [Fact]
    public void GetDescriptors_WhenBackendsAreKeyed_PublishesEveryRegisteredDescriptor()
    {
        using var provider = Compose(new DurableBackendKey("first"), new DurableBackendKey("second"));

        var catalog = new DurableBackendCatalog(provider);

        catalog.GetDescriptors()
            .Select(static descriptor => descriptor.Key)
            .ShouldBe([new DurableBackendKey("first"), new DurableBackendKey("second")], ignoreOrder: true);
    }

    [Fact]
    public void GetDescriptors_WhenReadRepeatedly_ReturnsTheSameSnapshotWithoutActivatingABackend()
    {
        // The catalog retains immutable descriptors, so a read never performs a live container lookup.
        using var provider = Compose(new DurableBackendKey("first"));
        var catalog = new DurableBackendCatalog(provider);

        var first = catalog.GetDescriptors();
        var second = catalog.GetDescriptors();

        second.ShouldBe(first);
    }

    private static ServiceProvider Compose(params DurableBackendKey[] keys)
    {
        var services = new ServiceCollection();
        foreach (var key in keys)
        {
            _ = services.AddKeyedSingleton<IDurableExecutionBackend>(
                key.Value,
                new InMemoryDurableExecutionBackend(key));
        }

        return services.BuildServiceProvider();
    }
}
