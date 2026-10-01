// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemoryEvaluationResultStore_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddInMemoryEvaluationResultStore(null!, new EvaluationResultStoreKey("k"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddInMemoryEvaluationResultStore(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddInMemoryEvaluationResultStore_WhenResolved_IsAKeyedSingletonPerKey()
    {
        using var provider = new ServiceCollection()
            .AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("a"))
            .AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("b"))
            .BuildServiceProvider();

        var a = provider.GetRequiredKeyedService<IEvaluationResultStore>("a");

        _ = a.ShouldBeOfType<InMemoryEvaluationResultStore>();
        provider.GetRequiredKeyedService<IEvaluationResultStore>("a").ShouldBeSameAs(a);
        provider.GetRequiredKeyedService<IEvaluationResultStore>("b").ShouldNotBeSameAs(a);
    }

    [Fact]
    public void AddInMemoryEvaluationResultStore_WhenRepeatedForTheSameKey_RegistersOnce()
    {
        var services = new ServiceCollection()
            .AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("a"))
            .AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("a"));

        services.Count(static d => d.ServiceType == typeof(IEvaluationResultStore)).ShouldBe(1);
    }

    [Fact]
    public void AddInMemoryEvaluationResultStore_WhenNothingIsRegistered_NoStoreExistsByDefault()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        provider.GetKeyedService<IEvaluationResultStore>("a").ShouldBeNull();
    }

    [Fact]
    public async Task AddInMemoryEvaluationResultStore_WhenResolvedThroughTheCore_IsSelectedByTheStoreSelector()
    {
        using var provider = new ServiceCollection()
            .AddAgentEvaluation()
            .AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("memory"))
            .BuildServiceProvider();

        var selection = await provider.GetRequiredService<IEvaluationResultStoreSelector>().SelectAsync(new EvaluationResultStoreKey("memory"), TestContext.Current.CancellationToken);

        _ = selection.ShouldBeOfType<EvaluationResultStoreSelected>().Store.ShouldBeOfType<InMemoryEvaluationResultStore>();
    }
}
