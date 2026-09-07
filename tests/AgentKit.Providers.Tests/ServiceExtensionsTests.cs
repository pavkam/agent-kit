// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>
/// Exercises registration idempotence, replaceability, and additive
/// descriptor-source composition through the public DI surface.
/// </summary>
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
            return services.AddModelDescriptors(
                new ModelDescriptorSourceId("app"),
                [ProviderTestData.Model("a")]);
        });

        var snapshot = await provider.GetRequiredService<IModelCatalog>()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.ConversationModels.ShouldHaveSingleItem().Alias.Value.ShouldBe("a");
    }

    [Fact]
    public async Task AddModelDescriptors_WhenCalledTwice_ComposesBothSourcesAdditively()
    {
        using var provider = Build(static services =>
        {
            _ = services.AddAgentProviders();
            _ = services.AddModelDescriptors(
                new ModelDescriptorSourceId("first"),
                [ProviderTestData.Model("a")]);
            return services.AddModelDescriptors(
                new ModelDescriptorSourceId("second"),
                [ProviderTestData.Model("b")]);
        });

        var snapshot = await provider.GetRequiredService<IModelCatalog>()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.ConversationModels.Length.ShouldBe(2);
    }

    [Fact]
    public async Task AddAgentProviders_WithNoDescriptorSource_ProducesEmptyCatalogRatherThanADefaultModel()
    {
        using var provider = Build(static services => services.AddAgentProviders());

        var snapshot = await provider.GetRequiredService<IModelCatalog>()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

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
        public ValueTask<ModelSelectionResult> SelectAsync(
            ModelSelectionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ModelSelectionResult>(new InvalidModelPolicy("custom"));
    }
}
