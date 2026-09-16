// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddLanguageTool_WhenCalledTwice_RegistersOneToolDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddLanguageTool();
        _ = services.AddLanguageTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(LanguageTool)).ShouldBe(1);
    }

    [Fact]
    public void AddLanguageTool_WhenConfigureProvided_AppliesConfiguredBounds()
    {
        var services = new ServiceCollection();
        _ = services.AddLanguageTool(static options => options.MaximumQueryCharacters = 4321);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<LanguageToolOptions>>().Value.MaximumQueryCharacters.ShouldBe(4321);
    }

    [Fact]
    public void AddLanguageTool_WhenBoundsAreValid_DoesNotThrowOnStart()
    {
        var services = new ServiceCollection();
        _ = services.AddLanguageTool();
        using var provider = services.BuildServiceProvider();

        _ = Should.NotThrow(() => provider.GetRequiredService<IOptions<LanguageToolOptions>>().Value);
    }

    [Theory]
    [InlineData(0, 100, 2, 5, 10, 10)]
    [InlineData(11, 10, 2, 5, 10, 10)]
    [InlineData(10, 100, 0, 5, 10, 10)]
    [InlineData(10, 100, 6, 5, 10, 10)]
    [InlineData(10, 100, 2, 5, 0, 10)]
    [InlineData(10, 100, 2, 5, 10, 0)]
    public void AddLanguageTool_WhenAnyBoundIsInvalid_ThrowsOptionsValidationException(
        int defaultMaximumResults,
        int maximumResults,
        int defaultTimeoutSeconds,
        int maximumTimeoutSeconds,
        int maximumQueryCharacters,
        int maximumTextCharacters)
    {
        var services = new ServiceCollection();
        _ = services.AddLanguageTool(options =>
        {
            options.DefaultMaximumResults = defaultMaximumResults;
            options.MaximumResults = maximumResults;
            options.DefaultTimeout = TimeSpan.FromSeconds(defaultTimeoutSeconds);
            options.MaximumTimeout = TimeSpan.FromSeconds(maximumTimeoutSeconds);
            options.MaximumQueryCharacters = maximumQueryCharacters;
            options.MaximumTextCharacters = maximumTextCharacters;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LanguageToolOptions>>().Value);
    }
}
