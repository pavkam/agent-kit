// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentProviders_RegistersTheThreeCoreServices()
    {
        using var provider = Build(static services => services.AddAgentProviders());
        _ = provider.GetRequiredService<IModelCatalog>();
        _ = provider.GetRequiredService<IModelSelector>();
        _ = provider.GetRequiredService<IModelCapabilityValidator>();
    }

    [Fact]
    public void AddAgentProviders_WhenCalledTwice_IsIdempotent()
    {
        using var provider = Build(static services =>
        {
            _ = services.AddAgentProviders();
            return services.AddAgentProviders();
        });
        provider.GetServices<IModelCatalog>().Count().ShouldBe(1);
        provider.GetServices<IModelSelector>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentProviders_WhenApplicationRegisteredItsOwnSelectorFirst_KeepsIt()
    {
        using var provider = Build(static services =>
        {
            services.TryAddSingleton<IModelSelector, CustomSelector>();
            return services.AddAgentProviders();
        });
        _ = provider.GetRequiredService<IModelSelector>().ShouldBeOfType<CustomSelector>();
    }

    [Fact]
    public async Task AddModelDescriptors_ContributesToTheComposedCatalog()
    {
        using var provider = Build(static services =>
        {
            _ = services.AddAgentProviders();
            return services.AddModelDescriptors(new ModelDescriptorSourceId("app"), [ProviderTestData.Model("a")]);
        });
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.ConversationModels.ShouldHaveSingleItem().Alias.Value.ShouldBe("a");
    }

    [Fact]
    public async Task AddModelDescriptors_WhenCalledTwice_ComposesBothSourcesAdditively()
    {
        using var provider = Build(static services =>
        {
            _ = services.AddAgentProviders();
            _ = services.AddModelDescriptors(new ModelDescriptorSourceId("first"), [ProviderTestData.Model("a")]);
            return services.AddModelDescriptors(new ModelDescriptorSourceId("second"), [ProviderTestData.Model("b")]);
        });
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.ConversationModels.Length.ShouldBe(2);
    }

    [Fact]
    public async Task AddModelDescriptorSource_RegistersTheCustomSourceAdditively()
    {
        using var provider = Build(static services =>
        {
            _ = services.AddAgentProviders();
            return services.AddModelDescriptorSource<CustomModelDescriptorSource>();
        });

        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.ConversationModels.ShouldHaveSingleItem().Alias.Value.ShouldBe("custom");
    }

    /// <summary>A minimal descriptor source used to verify AddModelDescriptorSource's DI registration.</summary>
    private sealed class CustomModelDescriptorSource: IModelDescriptorSource
    {
        public ModelDescriptorSourceId SourceId { get; } = new("custom-source");

        public ValueTask<ModelDescriptorSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ModelDescriptorSourceSnapshot(SourceId, new ModelDescriptorSourceVersion(1), [ProviderTestData.Model("custom")]));
    }

    [Fact]
    public async Task AddAgentProviders_WithNoDescriptorSource_ProducesEmptyCatalogRatherThanADefaultModel()
    {
        using var provider = Build(static services => services.AddAgentProviders());
        var snapshot = await provider.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.ConversationModels.ShouldBeEmpty();
    }

    private static ServiceProvider Build(Func<IServiceCollection, IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class CustomSelector: IModelSelector
    {
        public ValueTask<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult<ModelSelectionResult>(new InvalidModelPolicy("custom"));
    }

    [Fact]
    public void AddAgentProviders_WhenRegistered_ResolvesTheEmbeddingModelResolver()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        _ = services.AddSingleton<IEmbeddingModel>(new StubEmbeddingModel("embed"));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IEmbeddingModelResolver>().Resolve(ProviderTestData.EmbeddingModel("embed")).ShouldNotBeNull();
    }

    private sealed class StubEmbeddingModel(string alias): IEmbeddingModel
    {
        public EmbeddingModelAlias Alias { get; } = new(alias);

        public Task<EmbeddingAttemptResult> GenerateAsync(EmbeddingModelRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException("This stub never executes.");
    }

    [Fact]
    public void AddAgentProviders_WhenRegistered_ResolvesTheLlmModelResolver()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        _ = services.AddSingleton<ILlmModel>(new StubModel("chat"));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILlmModelResolver>().Resolve(ProviderTestData.Model("chat")).ShouldNotBeNull();
    }

    private sealed class StubModel(string alias): ILlmModel
    {
        public ModelAlias Alias { get; } = new(alias);

        public Task<ModelAttemptResult> ExecuteAsync(LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default) => throw new NotSupportedException("This stub never executes.");
    }
}
