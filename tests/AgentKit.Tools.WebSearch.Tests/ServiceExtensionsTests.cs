// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWebSearchTool_WhenCalledTwice_RegistersToolAndGeneratorOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool().AddWebSearchTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(WebSearchTool)).ShouldBe(1);
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
}
