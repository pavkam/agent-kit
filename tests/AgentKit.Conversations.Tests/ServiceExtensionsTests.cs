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
        var services = new ServiceCollection();
        RegisterCollaborators(services);
        _ = services.AddConversationSession(Configure);
        using var provider = services.BuildServiceProvider();

        var session = provider.GetRequiredService<IConversationSession>();

        _ = session.ShouldBeOfType<DefaultConversationSession>();
        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void AddConversationSession_WhenOptionsAreInvalid_ThrowsOptionsValidationExceptionOnResolution()
    {
        var services = new ServiceCollection();
        RegisterCollaborators(services);
        _ = services.AddConversationSession(options =>
        {
            Configure(options);
            options.AgentId = default;
        });
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IConversationSession>);

        exception.GetType().ShouldBe(typeof(OptionsValidationException));
    }

    [Fact]
    public void AddConversationSession_WhenCalled_RegistersDistinctIdentifierGeneratorsForEachTurnScopedIdentity()
    {
        var services = new ServiceCollection();
        RegisterCollaborators(services);
        _ = services.AddConversationSession(Configure);
        using var provider = services.BuildServiceProvider();

        var runIds = provider.GetRequiredService<IIdentifierGenerator<RunId>>();
        var operationIds = provider.GetRequiredService<IIdentifierGenerator<OperationId>>();
        var messageIds = provider.GetRequiredService<IIdentifierGenerator<MessageId>>();
        var sessionEntryIds = provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>();

        AssertDistinctNonDefault(runIds.Create, runIds.Create);
        AssertDistinctNonDefault(operationIds.Create, operationIds.Create);
        AssertDistinctNonDefault(messageIds.Create, messageIds.Create);
        AssertDistinctNonDefault(sessionEntryIds.Create, sessionEntryIds.Create);
    }

    [Fact]
    public void AddConversationSession_WhenLoggingIsNotRegistered_StillResolvesTheSession()
    {
        var services = new ServiceCollection();
        RegisterCollaborators(services);
        _ = services.AddConversationSession(Configure);
        using var provider = services.BuildServiceProvider();

        var session = provider.GetRequiredService<IConversationSession>();

        _ = session.ShouldNotBeNull();
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
        _ = services.AddSingleton<IAgentLoop>(new FakeAgentLoop());
        _ = services.AddSingleton<IContextAssembler>(new UnsupportedContextAssembler());
        _ = services.AddSingleton<IToolInvoker>(new CaptureTestToolInvoker());
        _ = services.AddSingleton<IModelCatalog>(new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])));
        _ = services.AddSingleton<IModelSelector>(ScriptedModelSelector.Selecting(FakeModelDescriptor()));
        _ = services.AddSingleton<ILlmModelResolver>(new AliasLlmModelResolver());
        services.TryAddKeyedSingleton<IRunContinuationPolicy>(
            AgentLoopComponentDefaults.ContinuationPolicyKeyValue, (_, _) => new UnsupportedRunContinuationPolicy());
    }

    private static ModelDescriptor FakeModelDescriptor()
    {
        var capabilities = new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: true,
            supportsParallelToolCalls: true,
            supportsStructuredOutput: true,
            supportsReasoning: true,
            supportsVisionInput: true,
            ExtensionData.Empty);

        return new ModelDescriptor(
            new ModelAlias("test-model"),
            new ProviderId("test-provider"),
            new ApiFamilyId("test-api"),
            new ModelId("test-model"),
            deploymentId: null,
            capabilities,
            new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024),
            pricing: null,
            ExtensionData.Empty);
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
