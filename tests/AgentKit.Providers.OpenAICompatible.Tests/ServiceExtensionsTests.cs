// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies the <c>AddOpenAICompatibleProvider</c> dependency-injection
/// registration: it registers the shared defaults exactly once and never
/// overrides an application's own replacement.
/// </summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOpenAICompatibleProvider_WhenCalled_RegistersDefaultTranslatorParserAndIdGenerator()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAICompatibleProvider();

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IOpenAIRequestTranslator>().ShouldBeOfType<OpenAIRequestTranslator>();
        _ = provider.GetRequiredService<IOpenAIStreamParser>().ShouldBeOfType<OpenAIChatCompletionResponseParser>();
        _ = provider.GetRequiredService<IIdentifierGenerator<ToolCallId>>().ShouldBeOfType<DefaultToolCallIdGenerator>();
    }

    [Fact]
    public void AddOpenAICompatibleProvider_WhenTranslatorAlreadyRegistered_DoesNotReplaceIt()
    {
        var services = new ServiceCollection();
        var customTranslator = new CustomTranslator();
        _ = services.AddSingleton<IOpenAIRequestTranslator>(customTranslator);

        _ = services.AddOpenAICompatibleProvider();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOpenAIRequestTranslator>().ShouldBeSameAs(customTranslator);
    }

    [Fact]
    public void AddOpenAICompatibleProvider_WhenCalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        _ = services.AddOpenAICompatibleProvider();
        _ = services.AddOpenAICompatibleProvider();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IOpenAIRequestTranslator>().ShouldHaveSingleItem();
    }

    private sealed class CustomTranslator: IOpenAIRequestTranslator
    {
        public JsonObject Translate(ChatModelRequest request, OpenAICompatibilityProfile profile, bool useStreaming) => [];
    }
}
