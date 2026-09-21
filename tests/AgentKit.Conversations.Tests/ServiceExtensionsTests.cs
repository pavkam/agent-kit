// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddConversationSession_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddConversationSession(Configure));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddConversationSession_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentNullException>(() => services.AddConversationSession(null!));

        exception.ParamName.ShouldBe("configure");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddConversationSession_WhenComposedWithRequiredCollaborators_ResolvesDefaultConversationSession()
    {
        using var provider = BuildProvider();

        var session = provider.GetRequiredService<IConversationSession>();

        _ = session.ShouldBeOfType<DefaultConversationSession>();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void AddConversationSession_WhenOptionsAreInvalid_ThrowsOptionsValidationExceptionOnResolution()
    {
        using var provider = BuildProvider(options =>
        {
            Configure(options);
            options.AgentId = default;
        });

        var exception = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IConversationSession>);

        exception.GetType().ShouldBe(typeof(OptionsValidationException));
    }

    [Fact]
    public void AddConversationSession_WhenCalled_RegistersDistinctOperationIdentifierGenerators()
    {
        using var provider = BuildProvider();

        var operationIds = provider.GetRequiredService<IIdentifierGenerator<OperationId>>();

        AssertDistinctNonDefault(operationIds.Create, operationIds.Create);
    }

    [Fact]
    public void AddConversationSession_WhenLoggingIsNotRegistered_StillResolvesTheSession()
    {
        using var provider = BuildProvider();

        var session = provider.GetRequiredService<IConversationSession>();

        _ = session.ShouldNotBeNull();
    }

    private static ServiceProvider BuildProvider(Action<ConversationSessionOptions>? configure = null)
    {
        var services = new ServiceCollection();
        RegisterCollaborators(services);
        _ = services.AddConversationSession(configure ?? Configure);
        ReplaceTurnExecutorWithFake(services);
        return services.BuildServiceProvider();
    }

    private static void ReplaceTurnExecutorWithFake(IServiceCollection services)
    {
        _ = services.RemoveAll<IConversationTurnExecutor>();
        _ = services.AddSingleton<IConversationTurnExecutor>(static sp =>
            new FakeConversationTurnExecutor(
                (FakeAgentLoop) sp.GetRequiredKeyedService<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue),
                (FakeSessionCoordinator) sp.GetRequiredService<ISessionCoordinator>()));
        _ = services.RemoveAll<IConversationEngineHost>();
    }

    private static void AssertDistinctNonDefault<TIdentifier>(Func<TIdentifier> first, Func<TIdentifier> second)
        where TIdentifier : struct
    {
        var firstValue = first();
        var secondValue = second();
        firstValue.ShouldNotBe(default);
        secondValue.ShouldNotBe(default);
        firstValue.ShouldNotBe(secondValue);
    }

    private static void RegisterCollaborators(IServiceCollection services)
    {
        _ = services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator());
        _ = services.AddSingleton<ISecurityProfileSelector>(new FakeSecurityProfileSelector());
        _ = services.AddKeyedSingleton<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, new FakeAgentLoop());
    }

    private static void Configure(ConversationSessionOptions options)
    {
        options.AgentId = ConversationSessionOptionsFactory.AgentId;
        options.Identity = ConversationSessionOptionsFactory.Identity;
        options.SecurityProfileKey = new SecurityProfileKey("test-security");
        options.ConfigurationVersion = new ConfigurationVersion(1);
        options.SessionProfile = TestSecurityEvidence.SessionProfile();
        options.ModelSelectionPolicy = new ModelSelectionPolicy([new ModelAlias("test-model")]);
    }
}
