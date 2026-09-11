// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class FacadeServiceCardinalityTests
{
    public static TheoryData<Type, string> RequiredServices => new()
    {
        { typeof(AgentEngine), "agentkit.engine" },
        { typeof(IAgentDefinitionCatalog), "agentkit.catalog" },
        { typeof(IAgentRunProfilePublicationReader), "agentkit.run-profile-reader" },
        { typeof(ISecurityProfileSelector), "agentkit.security-profile-selector" },
        { typeof(ISecurityGrantStore), "agentkit.security-grant-store" },
        { typeof(TimeProvider), "agentkit.time" },
        { typeof(IIdentifierGenerator<RunId>), "agentkit.runid" },
        { typeof(IIdentifierGenerator<OperationId>), "agentkit.operationid" },
        { typeof(IAgentLoop), "agentkit.loop" },
    };

    [Theory]
    [MemberData(nameof(RequiredServices))]
    public void Build_WhenSingularServiceIsDuplicated_RejectsBeforeAnyApplicationFactory(
        Type serviceType, string code)
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        _ = builder.Services.AddSingleton(serviceType, _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Duplicate service factories must never run.");
        });
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(_ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Catalog activation must follow cardinality validation.");
        }));

        // Act
        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        // Assert
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == $"{code}.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(RequiredServices))]
    public void CreateServiceProvider_WhenSingularServiceIsDuplicated_RejectsBeforeBuildingHostProvider(
        Type serviceType, string code)
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        _ = builder.Services.AddSingleton(serviceType, _ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Duplicate service factories must never run.");
        });
        var factory = new AgentKitServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = false,
        });

        // Act
        var exception = Should.Throw<AgentCompositionException>(() =>
            factory.CreateServiceProvider(factory.CreateBuilder(builder.Services)));

        // Assert
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == $"{code}.ambiguous");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(typeof(IAgentDefinitionCatalog), "agentkit.catalog")]
    [InlineData(typeof(IAgentRunProfilePublicationReader), "agentkit.run-profile-reader")]
    [InlineData(typeof(ISecurityProfileSelector), "agentkit.security-profile-selector")]
    [InlineData(typeof(ISecurityGrantStore), "agentkit.security-grant-store")]
    [InlineData(typeof(TimeProvider), "agentkit.time")]
    [InlineData(typeof(IIdentifierGenerator<RunId>), "agentkit.runid")]
    [InlineData(typeof(IIdentifierGenerator<OperationId>), "agentkit.operationid")]
    [InlineData(typeof(IAgentLoop), "agentkit.loop")]
    public void ValidateComponentRegistrations_WhenOnlyKeyedServiceRemains_ReportsMissingWithoutFactories(
        Type serviceType, string code)
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll(serviceType);
        var factoryCalls = 0;
        _ = builder.Services.AddKeyedSingleton(serviceType, "separate", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("A keyed service cannot satisfy an unkeyed requirement.");
        });

        // Act
        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(
                ComponentRegistrationSnapshot.Capture(builder.Services)));

        // Assert
        var expected = serviceType == typeof(IAgentLoop) ? "agentkit.loop.unresolvable" : $"{code}.missing";
        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == expected);
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(RequiredServices))]
    public async Task Build_WhenSingularServiceHasKeyedAlternatives_AcceptsWithoutActivatingAlternatives(
        Type serviceType, string code)
    {
        // Arrange
        _ = code;
        var builder = CompositionTestData.RunnableBuilder();
        var factoryCalls = 0;
        _ = builder.Services.AddKeyedSingleton(serviceType, "separate", (_, _) =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Unselected keyed alternatives must not run.");
        });

        // Act
        await using var engine = builder.Build();

        // Assert
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void ValidateComponentRegistrations_WhenSeveralServicesAreMissing_ReportsAllBeforeActivation()
    {
        // Arrange
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IAgentDefinitionCatalog>();
        _ = builder.Services.RemoveAll<TimeProvider>();
        _ = builder.Services.RemoveAll<ISecurityProfileSelector>();

        // Act
        var exception = Should.Throw<AgentCompositionException>(() =>
            AgentCompositionValidator.ValidateComponentRegistrations(
                ComponentRegistrationSnapshot.Capture(builder.Services)));

        // Assert
        exception.Diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(
            ["agentkit.catalog.missing", "agentkit.security-profile-selector.missing", "agentkit.time.missing"]);
    }

    [Fact]
    public void Build_WhenFacadeRegistrationIsRemoved_RejectsBeforeCatalogActivation()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<AgentEngine>();
        var factoryCalls = 0;
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(_ =>
        {
            factoryCalls++;
            throw new InvalidOperationException("A missing facade must fail before activation.");
        }));

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(static diagnostic => diagnostic.Code == "agentkit.engine.missing");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void CreateServiceProvider_WhenFacadeIsAbsent_DoesNotRequireRunnableSpine()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton(TimeProvider.System);
        var factory = new AgentKitServiceProviderFactory();

        using var provider = (ServiceProvider) factory.CreateServiceProvider(services);

        provider.GetServices<TimeProvider>().Count().ShouldBe(2);
    }
}
