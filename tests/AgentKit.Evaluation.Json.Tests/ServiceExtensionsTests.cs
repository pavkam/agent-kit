// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddJsonEvaluationResultStore_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        using var root = new JsonEvaluationTestRoot();

        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddJsonEvaluationResultStore(null!, new EvaluationResultStoreKey("k"), root.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonEvaluationResultStore(new EvaluationResultStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddJsonEvaluationResultStore(default, root.Target())).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddJsonEvaluationResultStore_WhenAConfiguredBoundIsInvalid_FailsAtRegistration()
    {
        using var root = new JsonEvaluationTestRoot();

        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonEvaluationResultStore(new EvaluationResultStoreKey("k"), root.Target(), static o => o.MaximumRecordBytes = 0))
            .ParamName.ShouldBe("maximumRecordBytes");
    }

    [Fact]
    public async Task AddJsonEvaluationResultStore_WhenResolved_ReturnsOneKeyedSingletonThatOpensLazily()
    {
        using var root = new JsonEvaluationTestRoot();
        await using var provider = new ServiceCollection()
            .AddJsonEvaluationResultStore(new EvaluationResultStoreKey("json"), root.Target())
            .AddJsonEvaluationResultStore(new EvaluationResultStoreKey("json"), root.Target())
            .BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IEvaluationResultStore>("json");

        _ = store.ShouldBeOfType<JsonEvaluationResultStore>();
        provider.GetRequiredKeyedService<IEvaluationResultStore>("json").ShouldBeSameAs(store);
        File.Exists(root.ManifestPath).ShouldBeFalse();
        _ = await store.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 1), TestContext.Current.CancellationToken);
        File.Exists(root.ManifestPath).ShouldBeTrue();
    }
}
