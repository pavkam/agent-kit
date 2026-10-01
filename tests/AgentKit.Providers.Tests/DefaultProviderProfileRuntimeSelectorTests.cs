// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DefaultProviderProfileRuntimeSelector"/>.</summary>
public sealed class DefaultProviderProfileRuntimeSelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenProfilesAndSourceAreRegistered_ReturnsSelectedLease()
    {
        using var provider = BuildProvider();
        var selector = provider.GetRequiredService<IProviderProfileRuntimeSelector>();
        var binding = Binding(provider);
        var result = await selector.SelectAsync(binding, Operation(), TestContext.Current.CancellationToken);
        var selected = result.ShouldBeOfType<ProviderProfileRuntimeSelected>();
        selected.Runtime.Endpoint.BaseAddress.ShouldBe(new Uri("https://api.example.test"));
        selected.Runtime.Credential.SourceKey.ShouldBe(new ProviderCredentialSourceKey("api-key"));
        _ = selected.Runtime.CredentialSource.ShouldBeOfType<StubCredentialSource>();
    }

    [Fact]
    public async Task SelectAsync_WhenEndpointProfileIsMissing_ReturnsUnavailable()
    {
        using var provider = BuildProvider(registerEndpoint: false);
        var selector = provider.GetRequiredService<IProviderProfileRuntimeSelector>();
        var credential = provider.GetRequiredService<ProviderProfileRegistry>().Credentials.Values.First();
        var binding = new ProviderOperationBinding(
            new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("missing"), new ProviderEndpointProfileVersion(1)),
            credential.Reference);
        var result = await selector.SelectAsync(binding, Operation(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<ProviderProfileRuntimeUnavailable>();
    }

    [Fact]
    public async Task SelectAsync_WhenSourceIsRegisteredOnlyUnderTheProviderIdentity_ReturnsUnavailableInsteadOfFallingBack()
    {
        var services = new ServiceCollection();
        _ = services.AddProviderEndpointProfile(new ProviderEndpointProfileKey("endpoint"), static options =>
        {
            options.ProviderId = new ProviderId("openai");
            options.ServiceSurface = new ProviderServiceSurfaceId("chat");
            options.EndpointId = new ProviderEndpointId("primary");
            options.BaseAddress = new Uri("https://api.example.test");
        });
        _ = services.AddProviderCredentialProfile(new ProviderCredentialProfileKey("credential"), static options =>
        {
            options.ProviderId = new ProviderId("openai");
            options.ServiceSurface = new ProviderServiceSurfaceId("chat");
            options.SourceKey = new ProviderCredentialSourceKey("api-key");
        });
        _ = services.AddKeyedSingleton<IProviderCredentialSource, StubCredentialSource>(new ProviderId("openai"));
        _ = services.AddAgentProviders();
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IProviderProfileRuntimeSelector>();

        var result = await selector.SelectAsync(Binding(provider), Operation(), TestContext.Current.CancellationToken);

        var unavailable = result.ShouldBeOfType<ProviderProfileRuntimeUnavailable>();
        unavailable.Failure.SafeMessage.ShouldBe("No credential source is registered for the selected credential profile.");
    }

    [Fact]
    public async Task SelectAsync_WhenSelected_ExposesTheSourceThatEnforcesCredentialReadGrants()
    {
        using var provider = BuildProvider();
        var selector = provider.GetRequiredService<IProviderProfileRuntimeSelector>();

        var result = await selector.SelectAsync(Binding(provider), Operation(), TestContext.Current.CancellationToken);

        var source = result.ShouldBeOfType<ProviderProfileRuntimeSelected>().Runtime.CredentialSource;
        source.Key.ShouldBe(new ProviderCredentialSourceKey("api-key"));
        source.SecurityAudience.ShouldBe(new ComponentId("test.credentials"));
    }

    private static ServiceProvider BuildProvider(bool registerEndpoint = true, bool registerCredential = true, bool registerSource = true)
    {
        var services = new ServiceCollection();
        if (registerEndpoint)
        {
            _ = services.AddProviderEndpointProfile(new ProviderEndpointProfileKey("endpoint"), static options =>
            {
                options.ProviderId = new ProviderId("openai");
                options.ServiceSurface = new ProviderServiceSurfaceId("chat");
                options.EndpointId = new ProviderEndpointId("primary");
                options.BaseAddress = new Uri("https://api.example.test");
            });
        }

        if (registerCredential)
        {
            _ = services.AddProviderCredentialProfile(new ProviderCredentialProfileKey("credential"), static options =>
            {
                options.ProviderId = new ProviderId("openai");
                options.ServiceSurface = new ProviderServiceSurfaceId("chat");
                options.SourceKey = new ProviderCredentialSourceKey("api-key");
            });
        }

        if (registerSource)
        {
            _ = services.AddProviderCredentialSource<StubCredentialSource>(new ProviderCredentialSourceKey("api-key"));
        }

        _ = services.AddAgentProviders();
        return services.BuildServiceProvider();
    }

    private static ProviderOperationBinding Binding(IServiceProvider provider)
    {
        var registry = provider.GetRequiredService<ProviderProfileRegistry>();
        var endpoint = registry.Endpoints.Values.First();
        var credential = registry.Credentials.Values.First();
        return new ProviderOperationBinding(endpoint.Reference, credential.Reference);
    }

    private static ProtectedSemanticOperationContext Operation()
    {
        var agentId = ProviderTestData.AgentId;
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(ProviderTestData.OperationId, admissionId: null);
        return new ProtectedSemanticOperationContext(
            agentId,
            ProviderTestData.SessionId,
            conversationId: null,
            identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, ProviderTestData.SessionId, correlation, identity));
    }

    private sealed class StubCredentialSource: IProviderCredentialSource
    {
        public ProviderCredentialSourceKey Key { get; } = new("api-key");

        public ComponentId SecurityAudience { get; } = new("test.credentials");

        public ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
            ProviderCredentialResolutionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Selector tests never resolve a credential.");
    }
}
