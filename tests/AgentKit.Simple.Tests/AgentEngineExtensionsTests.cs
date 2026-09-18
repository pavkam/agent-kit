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
    public async Task AskAsyncOfT_WhenTheModelAnswersWithValidJson_ReturnsTheDeserializedValue()
    {
        var handler = new StubOpenAIHandler(/*lang=json,strict*/ """{"category":"billing","priority":2}""");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<Triage>(/*lang=json,strict*/ """{"type":"object","properties":{"category":{"type":"string"},"priority":{"type":"integer"}},"required":["category","priority"],"additionalProperties":false}""");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var triage = await engine.AskAsync<Triage>("Classify: my invoice is wrong", TestContext.Current.CancellationToken);

        triage.ShouldBe(new Triage("billing", 2));
        handler.Bodies.Single().ShouldContain("JSON Schema");
        handler.Bodies.Single().ShouldContain("category");
        handler.Bodies.Single().ShouldContain("additionalProperties");
    }

    [Fact]
    public async Task AskAsyncOfT_WhenTheFirstAnswerIsInvalid_AsksTheModelToRepairAndReturnsTheSecond()
    {
        var handler = new StubOpenAIHandler("not json at all", /*lang=json,strict*/ """{"category":"billing","priority":1}""");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<Triage>(/*lang=json,strict*/ """{"type":"object","properties":{"category":{"type":"string"},"priority":{"type":"integer"}},"required":["category","priority"]}""");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var result = await engine.SendAsync("Classify this", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Output.ShouldNotBeNull().Value.ShouldBe(new Triage("billing", 1));
        handler.Bodies.Count.ShouldBe(2);
        handler.Bodies[1].ShouldContain("previous output was rejected");
        _ = result.Events.OfType<ConversationOutputEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AskAsyncOfT_WhenRepairsAreExhausted_ThrowsSimpleAgentExceptionWithTheRejection()
    {
        var handler = new StubOpenAIHandler("nope");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<Triage>(/*lang=json,strict*/ """{"type":"object"}""", maximumRepairAttempts: 1);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync<Triage>("Classify", TestContext.Current.CancellationToken));

        exception.Result.Succeeded.ShouldBeFalse();
        exception.Message.ShouldContain("rejected");
        handler.Bodies.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AskAsyncOfT_WhenNoOutputIsConfigured_ThrowsSimpleAgentExceptionNamingWithOutput()
    {
        await using var engine = Engine(new StubOpenAIHandler("plain text"));

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync<Triage>("hi", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("WithOutput<T>");
        exception.Result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AskAsyncOfT_WhenTheOutputIsAnotherType_ThrowsSimpleAgentException()
    {
        var handler = new StubOpenAIHandler(/*lang=json,strict*/ """{"category":"x","priority":1}""");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<Triage>(/*lang=json,strict*/ """{"type":"object"}""");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync<string>("hi", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Triage");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task AskAsyncOfT_WhenTextIsBlank_ThrowsArgumentException(string? text)
    {
        await using var engine = Engine(new StubOpenAIHandler("unused"));

        var exception = await Should.ThrowAsync<ArgumentException>(() => engine.AskAsync<Triage>(text!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("text");
    }

    private sealed record Triage(string Category, int Priority);

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
