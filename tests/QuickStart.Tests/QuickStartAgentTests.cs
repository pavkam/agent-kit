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
        using var agent = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(new ThrowingHandler()))));

        _ = agent.Conversation.ShouldBeAssignableTo<IConversationSession>();
    }

    [Fact]
    public async Task AskAsync_WhenTheModelAnswers_ReturnsTheAssistantTextAndSendAsyncTheUsage()
    {
        var handler = new StubOpenAIHandler("AgentKit composes agents from replaceable parts.");
        using var agent = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        var reply = await agent.AskAsync("What is AgentKit?", TestContext.Current.CancellationToken);
        var result = await agent.SendAsync("And again?", TestContext.Current.CancellationToken);

        reply.ShouldBe("AgentKit composes agents from replaceable parts.");
        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationUsageEvent>().ShouldHaveSingleItem().Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task AskAsync_WhenCalled_SendsTheBearerKeyTheSystemInstructionAndTheKnownModelId()
    {
        var handler = new StubOpenAIHandler("ok");
        using var agent = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        _ = await agent.AskAsync("hello", TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldNotBeNull().AbsoluteUri.ShouldStartWith("https://api.openai.com/");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-test");
        var body = handler.Bodies.Single();
        body.ShouldContain("\"model\":\"gpt-4o-mini\"");
        body.ShouldContain("You are a concise assistant.");
        body.ShouldContain("hello");
    }

    [Fact]
    public async Task AskAsync_WhenCalledTwice_ContinuesTheSameConversation()
    {
        var handler = new StubOpenAIHandler("ok");
        using var agent = QuickStartAgent.Create("sk-test", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(handler))));

        _ = await agent.AskAsync("first", TestContext.Current.CancellationToken);
        _ = await agent.AskAsync("second", TestContext.Current.CancellationToken);

        handler.Bodies.Count.ShouldBe(2);
        handler.Bodies[1].ShouldContain("first");
        handler.Bodies[1].ShouldContain("second");
    }

    [Fact]
    public async Task AskAsync_WhenTheProviderIsUnreachable_ThrowsSimpleAgentExceptionWithoutSecrets()
    {
        using var agent = QuickStartAgent.Create("sk-test-secret", services => services.Replace(
            ServiceDescriptor.Singleton(new HttpClient(new ThrowingHandler()))));

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => agent.AskAsync("hello", TestContext.Current.CancellationToken));

        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldNotContain("sk-test-secret");
        exception.Message.ShouldNotContain("HttpRequestException");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    private sealed class ThrowingHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused (sk-test-secret should never surface)");
    }
}
