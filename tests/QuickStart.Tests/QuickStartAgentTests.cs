// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace QuickStart.Tests;

/// <summary>Verifies the documented quick-start composition builds, validates, and completes a turn.</summary>
public sealed class QuickStartAgentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenApiKeyIsBlank_ThrowsArgumentExceptionBeforeComposing(string? apiKey)
    {
        var exception = Should.Throw<ArgumentException>(() => QuickStartAgent.Create(apiKey!));

        exception.ParamName.ShouldBe("apiKey");
    }

    [Fact]
    public void Create_WhenApiKeyIsSupplied_BuildsAValidatedCompositionWithoutTouchingTheNetwork()
    {
        using var conversation = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(new ThrowingHandler()))));

        _ = conversation.ShouldBeAssignableTo<IConversationSession>();
    }

    [Fact]
    public async Task SendAsync_WhenTheModelAnswers_ReturnsTheAssistantTextAndUsage()
    {
        var handler = new StubOpenAIHandler("AgentKit composes agents from replaceable parts.");
        using var conversation = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        var result = await conversation.SendAsync("What is AgentKit?", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text)
            .ShouldContain("AgentKit composes agents from replaceable parts.");
        result.Events.OfType<ConversationUsageEvent>().ShouldHaveSingleItem().Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task SendAsync_WhenCalled_SendsTheBearerKeyTheSystemInstructionAndTheKnownModelId()
    {
        var handler = new StubOpenAIHandler("ok");
        using var conversation = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        _ = await conversation.SendAsync("hello", TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldNotBeNull().AbsoluteUri.ShouldStartWith("https://api.openai.com/");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-test");
        var body = handler.Bodies.Single();
        body.ShouldContain("\"model\":\"gpt-4o-mini\"");
        body.ShouldContain("You are a concise assistant.");
        body.ShouldContain("hello");
    }

    [Fact]
    public async Task SendAsync_WhenCalledTwice_ContinuesTheSameConversation()
    {
        var handler = new StubOpenAIHandler("ok");
        using var conversation = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        _ = await conversation.SendAsync("first", TestContext.Current.CancellationToken);
        _ = await conversation.SendAsync("second", TestContext.Current.CancellationToken);

        handler.Bodies.Count.ShouldBe(2);
        handler.Bodies[1].ShouldContain("first");
        handler.Bodies[1].ShouldContain("second");
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderIsUnreachable_ReturnsAnUnsuccessfulResultWithoutSecrets()
    {
        using var conversation = QuickStartAgent.Create("sk-test-secret", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(new ThrowingHandler()))));

        var result = await conversation.SendAsync("hello", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var text = string.Join('\n', result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text));
        text.ShouldNotBeNullOrWhiteSpace();
        text.ShouldNotContain("sk-test-secret");
        text.ShouldNotContain("HttpRequestException");
    }

    private sealed class ThrowingHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused (sk-test-secret should never surface)");
    }
}
