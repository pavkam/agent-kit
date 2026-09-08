// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using Microsoft.Extensions.Logging;

public sealed class ComponentInfrastructureCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_WhenLoggingInfrastructureIsExplicitlySelected_ValidatesEquallyAcrossOwnershipModes(
        bool hostManaged)
    {
        if (hostManaged)
        {
            var services = CreateHostedServices();
            AddInfrastructure(services);
            await using var provider = CompositionTestData.BuildHostedProvider(services);

            _ = provider.GetRequiredService<AgentEngine>().ShouldNotBeNull();
            return;
        }

        var builder = CompositionTestData.RunnableBuilder();
        AddInfrastructure(builder.Services);
        await using var engine = builder.Build();

        _ = engine.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenSelectedInfrastructureIsOpaque_RejectsEquallyWithoutInvokingFactory(
        bool hostManaged)
    {
        var factoryCalls = 0;
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        _ = services.AddSingleton<IExternal>(
            _ =>
            {
                factoryCalls++;
                return new External();
            });
        _ = services.DeclareAgentKitComponent(RootRegistration(
            InfrastructureDependency<IExternal>()));

        var exception = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                _ = CompositionTestData.BuildHostedProvider(services);
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });

        exception.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-infrastructure.opaque-factory");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenInfrastructureExceedsConfiguredBound_RejectsEquallyAcrossOwnershipModes(
        bool hostManaged)
    {
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        AddInfrastructure(services);
        var compositionOptions = new AgentKitCompositionOptions(
            maximumDerivedInfrastructureRegistrations: 1);
        _ = standaloneBuilder?.CompositionOptions = compositionOptions;

        var exception = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                var factory = new AgentKitServiceProviderFactory(
                    compositionOptions,
                    new ServiceProviderOptions());
                _ = factory.CreateServiceProvider(factory.CreateBuilder(services));
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });

        exception.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.component-infrastructure.validation-bound-exceeded");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_WhenLaterBuildUsesLargerBound_CapturesFreshProviderLocalEvidence(bool hostManaged)
    {
        var standaloneBuilder = hostManaged ? null : CompositionTestData.RunnableBuilder();
        var services = hostManaged ? CreateHostedServices() : standaloneBuilder!.Services;
        AddInfrastructure(services);
        var limitedOptions = new AgentKitCompositionOptions(1);
        _ = standaloneBuilder?.CompositionOptions = limitedOptions;

        _ = Should.Throw<AgentCompositionException>(() =>
        {
            if (hostManaged)
            {
                var factory = new AgentKitServiceProviderFactory(
                    limitedOptions,
                    new ServiceProviderOptions());
                _ = factory.CreateServiceProvider(factory.CreateBuilder(services));
            }
            else
            {
                _ = standaloneBuilder!.Build();
            }
        });

        if (hostManaged)
        {
            var factory = new AgentKitServiceProviderFactory();
            await using var provider = (ServiceProvider) factory.CreateServiceProvider(
                factory.CreateBuilder(services));
            _ = provider.GetRequiredService<AgentEngine>();
        }
        else
        {
            standaloneBuilder!.CompositionOptions = new AgentKitCompositionOptions();
            await using var engine = standaloneBuilder.Build();
            _ = engine.ShouldNotBeNull();
        }
    }

    private static ServiceCollection CreateHostedServices()
    {
        var definition = CompositionTestData.Definition();
        var services = new ServiceCollection();
        _ = services.AddAgentKit();
        _ = services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        CompositionTestData.AddRunProfiles(services, definition);
        _ = services.AddAgent(definition);
        return services;
    }

    private static void AddInfrastructure(IServiceCollection services)
    {
        _ = services.AddLogging();
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        _ = services.DeclareAgentKitComponent(RootRegistration(
            InfrastructureDependency<ILogger<LoggerCategory>>(
                ComponentDependencyCardinality.OptionalSingular)));
    }

    private static ComponentRegistrationDescriptor RootRegistration(
        params ComponentDependencyDescriptor[] dependencies) => new(
            ComponentContractReference.Unkeyed<IInfrastructureRoot>(),
            typeof(InfrastructureRoot),
            ServiceLifetime.Singleton,
            [.. dependencies]);

    private static ComponentDependencyDescriptor InfrastructureDependency<TService>(
        ComponentDependencyCardinality cardinality = ComponentDependencyCardinality.RequiredSingular)
        where TService : class => new(
            ComponentContractReference.Unkeyed<TService>(),
            cardinality,
            factoryBoundary: null,
            ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure);

    private interface IInfrastructureRoot;

    private sealed class InfrastructureRoot: IInfrastructureRoot;

    private sealed class LoggerCategory;

    private interface IExternal;

    private sealed class External: IExternal;
}
