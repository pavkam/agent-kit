// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies AssistantMessage behavior and contracts.</summary>
public sealed class AssistantMessageTests
{
    private static readonly ImmutableArray<ContentPart> _sampleParts = [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)];
    private static AssistantResponseMetadata CreateResponse() => new(new ModelRequestId(Guid.NewGuid()), new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat-completions"), new ModelId("gpt"), new ModelId("gpt"), null, null, null), NormalizedStopReason.Completed, "stop", ModelUsage.NotReported, ExtensionData.Empty);
    [Fact]
    public void Constructor_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, MessageState.Complete, _sampleParts, null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesResponse()
    {
        var response = CreateResponse();
        var message = new AssistantMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, MessageState.Complete, _sampleParts, response, ExtensionData.Empty);
        message.Response.ShouldBe(response);
    }

    private static readonly Guid _messageGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _agentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _sessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _branchGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    [Fact]
    public void AssistantMessage_Equality_WhenSameValues_InstancesAreEqual() => new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ResponseMetadata(), ExtensionData.Empty).ShouldBe(new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ResponseMetadata(), ExtensionData.Empty));
    [Fact]
    public void AssistantMessage_Equality_WhenDifferentParts_InstancesAreNotEqual() => new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("a"), ResponseMetadata(), ExtensionData.Empty).ShouldNotBe(new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("b"), ResponseMetadata(), ExtensionData.Empty));
    private static MessageId MessageId => new(_messageGuid);
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static ImmutableArray<ContentPart> Parts(string text = "hi") => [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)];
    private static ProviderResponseIdentity ResponseIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null);
    private static AssistantResponseMetadata ResponseMetadata() => new(new ModelRequestId(_messageGuid), ResponseIdentity(), NormalizedStopReason.Completed, null, ModelUsage.NotReported, ExtensionData.Empty);
}
