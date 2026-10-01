// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

using System.Net.Http;

using AgentKit.TestSupport;


/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWebSearchTool_WhenCalledTwice_RegistersToolAndGeneratorOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool().AddWebSearchTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(RegisteredToolInvoker) && descriptor.ImplementationInstance is RegisteredToolInvoker marker && marker.Descriptor.Id == WebSearchTool.Id).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<WebSearchRequestId>)).ShouldBe(1);
        services.Any(descriptor => descriptor.ServiceType == typeof(IWebSearchProvider)).ShouldBeFalse();
    }

    [Fact]
    public void AddWebSearchTool_WhenConfigureProvided_AppliesConfiguredBounds()
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool(static options => options.MaximumDomains = 3);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<WebSearchToolOptions>>().Value.MaximumDomains.ShouldBe(3);
    }

    [Fact]
    public void AddWebSearchTool_WhenDefaultOptions_PassesValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool();
        using var provider = services.BuildServiceProvider();

        _ = Should.NotThrow(() => provider.GetRequiredService<IOptions<WebSearchToolOptions>>().Value);
    }

    [Theory]
    [InlineData(0, 10, 5, 10, 30, 120, 300, 2000)]
    [InlineData(1000, -1, 5, 10, 30, 120, 300, 2000)]
    [InlineData(1000, 10, 0, 10, 30, 120, 300, 2000)]
    [InlineData(1000, 10, 5, 4, 30, 120, 300, 2000)]
    [InlineData(1000, 10, 5, 10, 0, 120, 300, 2000)]
    [InlineData(1000, 10, 5, 10, 30, 10, 300, 2000)]
    [InlineData(1000, 10, 5, 10, 30, 120, 0, 2000)]
    [InlineData(1000, 10, 5, 10, 30, 120, 300, 0)]
    public void AddWebSearchTool_WhenAnyBoundIsInvalid_ThrowsOptionsValidationException(
        int maximumQueryCharacters,
        int maximumDomains,
        int defaultMaximumResults,
        int maximumResults,
        int defaultTimeoutSeconds,
        int maximumTimeoutSeconds,
        int maximumTitleCharacters,
        int maximumSnippetCharacters)
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool(options =>
        {
            options.MaximumQueryCharacters = maximumQueryCharacters;
            options.MaximumDomains = maximumDomains;
            options.DefaultMaximumResults = defaultMaximumResults;
            options.MaximumResults = maximumResults;
            options.DefaultTimeout = TimeSpan.FromSeconds(defaultTimeoutSeconds);
            options.MaximumTimeout = TimeSpan.FromSeconds(maximumTimeoutSeconds);
            options.MaximumTitleCharacters = maximumTitleCharacters;
            options.MaximumSnippetCharacters = maximumSnippetCharacters;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WebSearchToolOptions>>().Value);
    }

    [Fact]
    public async Task AddNetworkWebSearchProvider_WhenCollaboratorsRegistered_SendsOnlyThroughTheRegisteredTransport()
    {
        var grants = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(grants);
        var audit = new RecordingAuditDispatcher();
        var calls = 0;
        var transport = new CallbackNetworkTransport(
            grants,
            (_, _) =>
            {
                calls++;
                return ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(
                    new StreamNetworkResponse(200, new MemoryStream(/*lang=json,strict*/ """{"complete":true,"results":[]}"""u8.ToArray()))));
            });
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(authority))
            .AddSingleton<ISecurityGrantStore>(grants)
            .AddSingleton<ISecurityAuditDispatcher>(audit)
            .AddSingleton<INetworkNameResolver>(new FixedAddressNameResolver(grants, TimeProvider.System))
            .AddSingleton<INetworkTransport>(transport)
            .AddNetworkWebSearchProvider(static options => options.Endpoint = new Uri("https://search.example.test/query"));
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var search = provider.GetRequiredService<IWebSearchProvider>().ShouldBeOfType<NetworkWebSearchProvider>();
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        var context = TestData.Context;
        var decision = await authority.AuthorizeAsync(
            new SecurityRequest(
                new SecurityRequestId(Guid.Parse("21000000-0000-0000-0000-0000000000a1")),
                context.Authorization.Scope,
                context.ToolCallId,
                context.Authorization.Identity,
                context.Authorization,
                search.SecurityAudience,
                SecurityOperationKind.Network,
                SecurityEffect.Egress,
                [search.Destination],
                WebSearchSecurityBinding.Fingerprint(TestData.SearchId, search.ProviderId, search.Destination, "q", [], WebSearchFreshness.Any, 5, deadline),
                deadline),
            TestContext.Current.CancellationToken);
        var request = new WebSearchRequest(TestData.SearchId, context, "q", [], WebSearchFreshness.Any, 5, deadline, decision.ShouldBeOfType<SecurityAllowed>().Grant);

        var result = await search.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchSucceeded>();
        calls.ShouldBe(1);
        _ = audit.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddNetworkWebSearchProvider_WhenNetworkTransportMissing_FailsClosedInsteadOfCreatingHttpClient()
    {
        var grants = new ConsumingGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(new GrantingSecurityAuthority(grants)))
            .AddSingleton<ISecurityGrantStore>(grants)
            .AddSingleton<ISecurityAuditDispatcher>(new RecordingAuditDispatcher())
            .AddSingleton<INetworkNameResolver>(new FixedAddressNameResolver(grants, TimeProvider.System))
            .AddNetworkWebSearchProvider(static options => options.Endpoint = new Uri("https://search.example.test/query"));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IWebSearchProvider>);
    }

    [Fact]
    public void AddNetworkWebSearchProvider_WhenRegistered_ReferencesNoHttpClientOrHandlerServices()
    {
        var services = new ServiceCollection();

        _ = services.AddNetworkWebSearchProvider(static options => options.Endpoint = new Uri("https://search.example.test/query"));

        services.Any(static descriptor =>
                descriptor.ServiceType == typeof(HttpClient)
                || descriptor.ServiceType == typeof(HttpMessageHandler)
                || descriptor.ServiceType == typeof(SocketsHttpHandler)
                || descriptor.ServiceType.FullName == "System.Net.Http.IHttpClientFactory")
            .ShouldBeFalse();
        typeof(NetworkWebSearchProvider).GetInterfaces().ShouldNotContain(typeof(IDisposable));
    }

    [Fact]
    public void AddNetworkWebSearchProvider_WhenProviderAlreadyRegistered_KeepsTheEarlierProvider()
    {
        var services = new ServiceCollection();
        var custom = new EnforcingSearchProvider();
        _ = services.AddSingleton<IWebSearchProvider>(custom)
            .AddNetworkWebSearchProvider(static options => options.Endpoint = new Uri("https://search.example.test/query"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWebSearchProvider>().ShouldBeSameAs(custom);
    }

    [Fact]
    public void AddNetworkWebSearchProvider_WhenGeneratorAlreadyRegistered_KeepsTheReplacement()
    {
        var services = new ServiceCollection();
        var replacement = new FixedIdentifierGenerator<NetworkOperationId>(new NetworkOperationId(Guid.Parse("31000000-0000-0000-0000-000000000009")));
        _ = services.AddSingleton<IIdentifierGenerator<NetworkOperationId>>(replacement)
            .AddNetworkWebSearchProvider(static options => options.Endpoint = new Uri("https://search.example.test/query"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IIdentifierGenerator<NetworkOperationId>>().ShouldBeSameAs(replacement);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<NetworkOperationId>)).ShouldBe(1);
    }

    [Theory]
    [InlineData(null, 262_144, 10, 1)]
    [InlineData("http://search.example.test/", 262_144, 10, 1)]
    [InlineData("https://user:pw@search.example.test/", 262_144, 10, 1)]
    [InlineData("https://search.example.test/#fragment", 262_144, 10, 1)]
    [InlineData("https://search.example.test/", 0, 10, 1)]
    [InlineData("https://search.example.test/", 262_144, 0, 1)]
    [InlineData("https://search.example.test/", 262_144, 10, 99)]
    public void AddNetworkWebSearchProvider_WhenOptionIsInvalid_ThrowsOptionsValidationException(
        string? endpoint,
        int maximumResponseBytes,
        int connectTimeoutSeconds,
        int classification)
    {
        var services = new ServiceCollection();
        _ = services.AddNetworkWebSearchProvider(options =>
        {
            options.Endpoint = endpoint is null ? null : new Uri(endpoint);
            options.MaximumResponseBytes = maximumResponseBytes;
            options.ConnectTimeout = TimeSpan.FromSeconds(connectTimeoutSeconds);
            options.Classification = (NetworkDataClassification) classification;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NetworkWebSearchProviderOptions>>().Value);
    }

    [Fact]
    public void AddNetworkWebSearchProvider_WhenArgumentIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddNetworkWebSearchProvider(null!, static _ => { }))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddNetworkWebSearchProvider(null!))
            .ParamName.ShouldBe("configure");
    }
}
