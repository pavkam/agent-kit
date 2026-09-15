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
    public void CreateBuilder_WhenApiKeyIsBlank_ThrowsArgumentExceptionBeforeComposing(string? apiKey)
    {
        var exception = Should.Throw<ArgumentException>(() => QuickStartAgent.CreateBuilder(apiKey!));

        exception.ParamName.ShouldBe("apiKey");
    }

    [Fact]
    public async Task Build_WhenApiKeyIsSupplied_ProducesAValidatedEngineWithoutTouchingTheNetwork()
    {
        await using var engine = Engine(new ThrowingHandler());

        _ = engine.Conversation.ShouldBeAssignableTo<IConversationSession>();
        _ = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AskAsync_WhenTheModelAnswers_ReturnsTheAssistantTextAndSendAsyncTheUsage()
    {
        var handler = new StubOpenAIHandler("AgentKit composes agents from replaceable parts.");
        await using var engine = Engine(handler);

        var reply = await engine.AskAsync("What is AgentKit?", TestContext.Current.CancellationToken);
        var result = await engine.SendAsync("And again?", TestContext.Current.CancellationToken);

        reply.ShouldBe("AgentKit composes agents from replaceable parts.");
        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationUsageEvent>().ShouldHaveSingleItem().Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task AskAsync_WhenCalled_SendsTheBearerKeyTheSystemInstructionAndTheKnownModelId()
    {
        var handler = new StubOpenAIHandler("ok");
        await using var engine = Engine(handler);

        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

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
        await using var engine = Engine(handler);

        _ = await engine.AskAsync("first", TestContext.Current.CancellationToken);
        _ = await engine.AskAsync("second", TestContext.Current.CancellationToken);

        handler.Bodies.Count.ShouldBe(2);
        handler.Bodies[1].ShouldContain("first");
        handler.Bodies[1].ShouldContain("second");
    }

    [Fact]
    public async Task AskAsync_WhenTheProviderIsUnreachable_ThrowsSimpleAgentExceptionWithoutSecrets()
    {
        await using var engine = Engine(new ThrowingHandler(), apiKey: "sk-test-secret");

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("hello", TestContext.Current.CancellationToken));

        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldNotContain("sk-test-secret");
        exception.Message.ShouldNotContain("HttpRequestException");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    private static AgentEngine Engine(HttpMessageHandler handler, string apiKey = "sk-test")
    {
        var builder = QuickStartAgent.CreateBuilder(apiKey);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        return builder.Build();
    }

    private sealed class ThrowingHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused (sk-test-secret should never surface)");
    }
}
