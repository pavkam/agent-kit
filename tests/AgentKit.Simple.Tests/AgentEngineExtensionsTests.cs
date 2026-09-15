// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Verifies AgentEngineExtensions behavior and contracts.</summary>
public sealed class AgentEngineExtensionsTests
{
    [Fact]
    public async Task AskAsync_WhenEngineIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => ((AgentEngine) null!).AskAsync("hi", TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("engine");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AskAsync_WhenTextIsBlank_ThrowsArgumentException(string? text)
    {
        await using var engine = Engine(new StubOpenAIHandler("unused"));

        var exception = await Should.ThrowAsync<ArgumentException>(() => engine.AskAsync(text!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public async Task SendAsync_WhenObserverIsNull_ThrowsArgumentNullException()
    {
        await using var engine = Engine(new StubOpenAIHandler("unused"));

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => engine.SendAsync("hi", null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("observer");
    }

    [Fact]
    public async Task AskAsync_WhenTheModelAnswers_ReturnsTheAssistantText()
    {
        await using var engine = Engine(new StubOpenAIHandler("Hello there."));

        var reply = await engine.AskAsync("hi", TestContext.Current.CancellationToken);

        reply.ShouldBe("Hello there.");
    }

    [Fact]
    public async Task AskAsync_WhenCalledTwice_ContinuesOneConversation()
    {
        var handler = new StubOpenAIHandler("one", "two");
        await using var engine = Engine(handler);

        var first = await engine.AskAsync("first question", TestContext.Current.CancellationToken);
        var second = await engine.AskAsync("second question", TestContext.Current.CancellationToken);

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
        await using var engine = Engine(new StubOpenAIHandler("""tool:nope:{"a":1}"""), maxTurns: 1);

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("do it", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("turn limit");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task AskAsync_WhenTheProviderIsUnreachable_ThrowsSimpleAgentExceptionWithoutTheKey()
    {
        await using var engine = Engine(new ThrowingHandler(), apiKey: "sk-super-secret");

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("hi", TestContext.Current.CancellationToken));

        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldNotContain("sk-super-secret");
        exception.Message.ShouldNotContain("HttpRequestException");
        exception.Result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenCalled_ReturnsTheFullTurnIncludingUsage()
    {
        await using var engine = Engine(new StubOpenAIHandler("ok"));

        var result = await engine.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationUsageEvent>().ShouldHaveSingleItem().Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task SendAsync_WhenAnObserverIsSupplied_StreamsTextBeforeCompletion()
    {
        await using var engine = Engine(new StubOpenAIHandler("streamed"));
        var observer = new RecordingObserver();

        _ = await engine.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ConversationAssistantTextDeltaEvent>().Select(static e => e.Text).ShouldContain("streamed");
        _ = observer.Events.Last().ShouldBeOfType<ConversationTurnCompletedEvent>();
    }

    [Fact]
    public async Task Conversation_WhenEngineHostsNoConversation_ThrowsInvalidOperationExceptionNamingTheFix()
    {
        // An engine without a conversation is still a valid engine; only the AskAsync surface is absent.
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.RemoveAll<IConversationSession>();
        await using var engine = builder.Build();

        var exception = Should.Throw<InvalidOperationException>(() => engine.Conversation);

        exception.Message.ShouldContain("UseLocalDevelopmentDefaults");
    }

    private static AgentEngine Engine(HttpMessageHandler handler, int maxTurns = 4, string apiKey = "sk-test")
    {
        var builder = AgentEngine.CreateBuilder()
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
