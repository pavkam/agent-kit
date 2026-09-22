// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

using AgentKit.TestSupport;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWebFetchTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddWebFetchTool()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddWebFetchTool_WhenCalledTwice_RegistersOneTool()
    {
        var services = CreateServices();

        _ = services.AddWebFetchTool();
        _ = services.AddWebFetchTool();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITool>().Count(static tool => tool is WebFetchTool).ShouldBe(1);
    }

    [Fact]
    public void AddWebFetchTool_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = CreateServices();

        _ = services.AddWebFetchTool(static options => options.MaximumRedirects = 3);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<WebFetchToolOptions>>().Value.MaximumRedirects.ShouldBe(3);
    }

    [Theory]
    [InlineData(nameof(WebFetchToolOptions.DefaultMaximumCharacters), 0)]
    [InlineData(nameof(WebFetchToolOptions.MaximumResponseBytes), 0)]
    [InlineData(nameof(WebFetchToolOptions.ConnectTimeout), 0)]
    [InlineData(nameof(WebFetchToolOptions.MaximumRedirects), -1)]
    [InlineData(nameof(WebFetchToolOptions.MaximumUrlCharacters), 0)]
    [InlineData(nameof(WebFetchToolOptions.MaximumHeaderCount), 0)]
    [InlineData(nameof(WebFetchToolOptions.MaximumHeaderCharacters), 0)]
    public void AddWebFetchTool_WhenBoundIsInvalid_FailsOptionsValidation(string property, int invalidValue)
    {
        var services = CreateServices();
        _ = services.AddWebFetchTool(options => Apply(options, property, invalidValue));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<WebFetchToolOptions>>().Value);
    }

    [Fact]
    public void AddWebFetchTool_WhenTimeoutBoundsInvalid_FailsOptionsValidation()
    {
        var services = CreateServices();
        _ = services.AddWebFetchTool(static options => options.DefaultTimeout = options.MaximumTimeout + TimeSpan.FromSeconds(1));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<WebFetchToolOptions>>().Value);
    }

    [Fact]
    public void AddWebFetchTool_WhenBoundsAreValid_ResolvesTheTool()
    {
        var services = CreateServices();
        _ = services.AddWebFetchTool();
        using var provider = services.BuildServiceProvider();

        var tool = provider.GetServices<ITool>().OfType<WebFetchTool>().ShouldHaveSingleItem();
        ((ITool) tool).Descriptor.Id.ShouldBe(WebFetchTool.Id);
    }

    private static void Apply(WebFetchToolOptions options, string property, int value)
    {
        switch (property)
        {
            case nameof(WebFetchToolOptions.DefaultMaximumCharacters):
                options.DefaultMaximumCharacters = value;
                break;
            case nameof(WebFetchToolOptions.MaximumResponseBytes):
                options.MaximumResponseBytes = value;
                break;
            case nameof(WebFetchToolOptions.ConnectTimeout):
                options.ConnectTimeout = TimeSpan.FromMilliseconds(value);
                break;
            case nameof(WebFetchToolOptions.MaximumRedirects):
                options.MaximumRedirects = value;
                break;
            case nameof(WebFetchToolOptions.MaximumUrlCharacters):
                options.MaximumUrlCharacters = value;
                break;
            case nameof(WebFetchToolOptions.MaximumHeaderCount):
                options.MaximumHeaderCount = value;
                break;
            case nameof(WebFetchToolOptions.MaximumHeaderCharacters):
                options.MaximumHeaderCharacters = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unexpected option property.");
        }
    }

    private static ServiceCollection CreateServices()
    {
        var store = new StrictGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<INetworkNameResolver>(new ScriptedNetworkNameResolver(store, new FixedTimeProvider()));
        _ = services.AddSingleton<INetworkTransport>(new ScriptedNetworkTransport(store, new FixedTimeProvider()));
        var authority = new RecordingSecurityAuthority(store);
        _ = services.AddSingleton<ISecurityAuthority>(authority);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(authority));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, SequenceSecurityRequestIdGenerator>();
        _ = services.AddSingleton<IIdentifierGenerator<NetworkOperationId>, SequenceNetworkOperationIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
