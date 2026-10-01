// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

using AgentKit.Tests;

public sealed class EngineAgentMessageChannelTests
{
    private static readonly AgentId _recipient = new(Guid.Parse("a0000000-0000-0000-0000-00000000000c"));

    private static async Task<(AgentEngine Engine, IAgentMessageChannel Channel, SessionId Session)> BuildAsync()
    {
        var builder = CompositionTestData.SendableBuilder(
            new GatedAgentLoop(), new InMemoryTestSessionCoordinator(), SessionBusyBehavior.Reject,
            CompositionTestData.Definition(), CompositionTestData.Definition(_recipient, "recipient"));
        _ = builder.Services.AddLogging();
        CompositionTestData.UseFirstPartyIo(builder.Services);
        _ = builder.Services.AddAgentMessageChannel();
        var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(_recipient, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;
        var seeded = await agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hello"), TestContext.Current.CancellationToken);
        return (engine, engine.Services.GetRequiredService<IAgentMessageChannel>(), seeded.SessionId);
    }

    private static AgentMessageRequest Message(SessionId recipientSession, string text = "Please double-check the parser.", string key = "m1", InputDelivery delivery = InputDelivery.Steer, AgentId? recipient = null, GoalId? goal = null, GoalAttemptId? attempt = null) => new(
        CompositionTestData.AgentId, CompositionTestData.SessionId, GoalTestData.NewRun(), recipient ?? _recipient, recipientSession, goal, attempt, delivery,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], new IdempotencyKey(key), CompositionTestData.Identity());

    private static AgentMessageAccepted Accepted(AgentMessageResult result)
    {
        (result as AgentMessageRejected)?.SafeReason.ShouldBeNull();
        return result.ShouldBeOfType<AgentMessageAccepted>();
    }

    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EngineAgentMessageChannel(null!)).ParamName.ShouldBe("services");

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var (engine, channel, _) = await BuildAsync();
        await using var owned = engine;

        (await Should.ThrowAsync<ArgumentNullException>(async () => await channel.SendAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SendAsync_WhenRecipientIsHosted_AdmitsTheMessageAndAReplayUsesTheSameInputIdentity()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;
        var request = Message(session, goal: new GoalId(Guid.NewGuid()), attempt: new GoalAttemptId(Guid.NewGuid()));

        var first = Accepted(await channel.SendAsync(request, TestContext.Current.CancellationToken));
        var replay = Accepted(await channel.SendAsync(request, TestContext.Current.CancellationToken));

        first.Receipt.AgentId.ShouldBe(_recipient);
        first.Receipt.SessionId.ShouldBe(session);
        replay.Receipt.InputId.ShouldBe(first.Receipt.InputId);
    }

    [Fact]
    public async Task SendAsync_WhenTheSameKeyCarriesDifferentContent_UsesTheSameInputIdentitySoTheRecipientsStoreOwnsTheConflict()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;

        var first = Accepted(await channel.SendAsync(Message(session, "first"), TestContext.Current.CancellationToken));
        var second = await channel.SendAsync(Message(session, "second"), TestContext.Current.CancellationToken);

        (second is AgentMessageRejected { Kind: AgentMessageRejectionKind.Conflict } || (second is AgentMessageAccepted accepted && accepted.Receipt.InputId == first.Receipt.InputId)).ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenDifferentKeysAreUsed_AdmitsDistinctInputs()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;

        var first = Accepted(await channel.SendAsync(Message(session, key: "a"), TestContext.Current.CancellationToken));
        var second = Accepted(await channel.SendAsync(Message(session, key: "b"), TestContext.Current.CancellationToken));

        second.Receipt.InputId.ShouldNotBe(first.Receipt.InputId);
    }

    [Fact]
    public async Task SendAsync_WhenDeliveryIsFollowUp_AdmitsItAsFollowUp()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;

        var result = await channel.SendAsync(Message(session, delivery: InputDelivery.FollowUp), TestContext.Current.CancellationToken);

        _ = Accepted(result);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientIsNotHosted_RejectsAsUnknownRecipientWithoutAdmitting()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;

        var result = await channel.SendAsync(Message(session, recipient: new AgentId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<AgentMessageRejected>().Kind.ShouldBe(AgentMessageRejectionKind.UnknownRecipient);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientSessionDoesNotExist_RejectsWithoutAdmitting()
    {
        var (engine, channel, _) = await BuildAsync();
        await using var owned = engine;

        var result = await channel.SendAsync(Message(new SessionId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<AgentMessageRejected>().Kind.ShouldBe(AgentMessageRejectionKind.Rejected);
    }

    [Fact]
    public async Task SendAsync_WhenCancelled_EmitsCancelledActivityAndThrows()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;
        var request = Message(session, key: "cancel");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.AgentMessageSend && Equals(activity.GetTagItem(AgentKitTagNames.RunId), request.SenderRunId.ToString()));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await channel.SendAsync(request, cancel.Token));

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task SendAsync_WhenAdmitted_EmitsOkActivityAndBoundedMetricWithoutContent()
    {
        var (engine, channel, session) = await BuildAsync();
        await using var owned = engine;
        var request = Message(session, "SECRET-CONTENT", "observe");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.AgentMessageSend && Equals(activity.GetTagItem(AgentKitTagNames.RunId), request.SenderRunId.ToString()));
        using var metrics = new MetricCollector(AgentKitMetricNames.AgentMessageCount);

        _ = await channel.SendAsync(request, TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.Tags.Values.ShouldAllBe(static value => value == null || !value.ToString()!.Contains("SECRET-CONTENT", StringComparison.Ordinal));
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "accepted"));
    }

    [Fact]
    public void AddAgentMessageChannel_WhenCalledTwice_RegistersOneChannel()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentMessageChannel().AddAgentMessageChannel();

        services.Count(static descriptor => descriptor.ServiceType == typeof(IAgentMessageChannel)).ShouldBe(1);
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddAgentMessageChannel()).ParamName.ShouldBe("services");
    }
}
