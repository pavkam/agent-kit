// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Verifies SimpleAgent behavior and contracts.</summary>
public sealed class SimpleAgentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AskAsync_WhenTextIsBlank_ThrowsArgumentException(string? text)
    {
        using var agent = Agent(new StubOpenAIHandler("unused"));

        var exception = await Should.ThrowAsync<ArgumentException>(() => agent.AskAsync(text!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public async Task SendAsync_WhenObserverIsNull_ThrowsArgumentNullException()
    {
        using var agent = Agent(new StubOpenAIHandler("unused"));

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => agent.SendAsync("hi", null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("observer");
    }

    [Fact]
    public async Task AskAsync_WhenTheModelAnswers_ReturnsTheAssistantText()
    {
        using var agent = Agent(new StubOpenAIHandler("Hello there."));

        var reply = await agent.AskAsync("hi", TestContext.Current.CancellationToken);

        reply.ShouldBe("Hello there.");
    }

    [Fact]
    public async Task AskAsync_WhenCalledTwice_ContinuesOneConversation()
    {
        var handler = new StubOpenAIHandler("one", "two");
        using var agent = Agent(handler);

        var first = await agent.AskAsync("first question", TestContext.Current.CancellationToken);
        var second = await agent.AskAsync("second question", TestContext.Current.CancellationToken);

        first.ShouldBe("one");
        second.ShouldBe("two");
        handler.Bodies[1].ShouldContain("first question");
        handler.Bodies[1].ShouldContain("one");
        handler.Bodies[1].ShouldContain("second question");
    }

    [Fact]
    public async Task AskAsync_WhenTheRunHitsItsTurnLimit_ThrowsSimpleAgentExceptionWithTheSafeReason()
    {
        // The model keeps requesting an unknown tool; with MaxTurns = 1 the run stops at the limit.
        using var agent = Agent(new StubOpenAIHandler("""tool:nope:{"a":1}"""), maxTurns: 1);

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => agent.AskAsync("do it", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("turn limit");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task AskAsync_WhenTheProviderIsUnreachable_ThrowsSimpleAgentExceptionWithoutTheKey()
    {
        using var agent = Agent(new ThrowingHandler(), apiKey: "sk-super-secret");

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => agent.AskAsync("hi", TestContext.Current.CancellationToken));

        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldNotContain("sk-super-secret");
        exception.Message.ShouldNotContain("HttpRequestException");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenCalled_ReturnsTheFullTurnIncludingUsage()
    {
        using var agent = Agent(new StubOpenAIHandler("ok"));

        var result = await agent.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationUsageEvent>().ShouldHaveSingleItem().Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task SendAsync_WhenAnObserverIsSupplied_StreamsTextBeforeCompletion()
    {
        using var agent = Agent(new StubOpenAIHandler("streamed"));
        var observer = new RecordingObserver();

        _ = await agent.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ConversationAssistantTextDeltaEvent>().Select(static e => e.Text).ShouldContain("streamed");
        _ = observer.Events.Last().ShouldBeOfType<ConversationTurnCompletedEvent>();
    }

    [Fact]
    public async Task Dispose_WhenCalled_RejectsFurtherCallsAndIsIdempotent()
    {
        var agent = Agent(new StubOpenAIHandler("ok"));

        agent.Dispose();
        agent.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(() => agent.AskAsync("hi", TestContext.Current.CancellationToken));
    }

    private static SimpleAgent Agent(HttpMessageHandler handler, int maxTurns = 4, string apiKey = "sk-test")
    {
        var builder = SimpleAgentBuilder.Create()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI(apiKey, "gpt-4o-mini")
            .WithInstructions("Be brief.")
            .WithMaxTurns(maxTurns);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        return builder.Build();
    }

    private sealed class ThrowingHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("refused; sk-super-secret must not surface");
    }

    private sealed class RecordingObserver: IConversationEventObserver
    {
        public List<ConversationEvent> Events { get; } = [];

        public ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(conversationEvent);
            return ValueTask.CompletedTask;
        }
    }
}
