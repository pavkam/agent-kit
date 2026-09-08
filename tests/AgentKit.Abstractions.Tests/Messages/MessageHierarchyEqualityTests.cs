// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>
/// Exercises the structural equality of every concrete <see cref="AgentMessage"/>
/// kind, proving the shared base override in <see cref="AgentMessage"/>
/// compares <see cref="AgentMessage.Parts"/> by content for every kind.
/// </summary>
public sealed class MessageHierarchyEqualityTests
{
    private static readonly Guid _messageGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _agentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _sessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _branchGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void SystemMessage_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new SystemMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty);
        var second = new SystemMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SystemMessage_Equality_WhenDifferentParts_InstancesAreNotEqual() =>
        new SystemMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("a"), ExtensionData.Empty)
            .ShouldNotBe(new SystemMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("b"), ExtensionData.Empty));

    [Fact]
    public void DeveloperMessage_Equality_WhenSameValues_InstancesAreEqual() =>
        new DeveloperMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty)
            .ShouldBe(new DeveloperMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));

    [Fact]
    public void UserMessage_Equality_WhenSameValues_InstancesAreEqual() =>
        new UserMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty)
            .ShouldBe(new UserMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));

    [Fact]
    public void ToolMessage_Equality_WhenSameValues_InstancesAreEqual() =>
        new ToolMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty)
            .ShouldBe(new ToolMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));

    [Fact]
    public void RuntimeMessage_Equality_WhenSameValues_InstancesAreEqual() =>
        new RuntimeMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty)
            .ShouldBe(new RuntimeMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));

    [Fact]
    public void AssistantMessage_Equality_WhenSameValues_InstancesAreEqual() =>
        new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ResponseMetadata(), ExtensionData.Empty)
            .ShouldBe(new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ResponseMetadata(), ExtensionData.Empty));

    [Fact]
    public void AssistantMessage_Equality_WhenDifferentParts_InstancesAreNotEqual() =>
        new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("a"), ResponseMetadata(), ExtensionData.Empty)
            .ShouldNotBe(new AssistantMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts("b"), ResponseMetadata(), ExtensionData.Empty));

    [Fact]
    public void ModelUsage_Equality_WhenSameValues_InstancesAreEqual() =>
        new ModelUsage(1, 2, null, null, null, null, ExtensionData.Empty).ShouldBe(
            new ModelUsage(1, 2, null, null, null, null, ExtensionData.Empty));

    [Fact]
    public void ProviderResponseIdentity_Constructor_WhenValid_RoundTripsProperties()
    {
        var identity = ResponseIdentity();

        identity.ProviderId.ShouldBe(new ProviderId("openai"));
    }

    [Fact]
    public void ProviderResponseIdentity_Equality_WhenSameValues_InstancesAreEqual() =>
        ResponseIdentity().ShouldBe(ResponseIdentity());

    [Fact]
    public void AssistantResponseMetadata_Equality_WhenSameValues_InstancesAreEqual() =>
        ResponseMetadata().ShouldBe(ResponseMetadata());

    private static MessageId MessageId => new(_messageGuid);

    private static AgentId AgentId => new(_agentGuid);

    private static SessionId SessionId => new(_sessionGuid);

    private static BranchId BranchId => new(_branchGuid);

    private static ImmutableArray<ContentPart> Parts(string text = "hi") =>
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)];

    private static ProviderResponseIdentity ResponseIdentity() => new(
        new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null);

    private static AssistantResponseMetadata ResponseMetadata() => new(
        new ModelRequestId(_messageGuid),
        ResponseIdentity(),
        NormalizedStopReason.Completed,
        null,
        ModelUsage.Empty, ExtensionData.Empty);
}
