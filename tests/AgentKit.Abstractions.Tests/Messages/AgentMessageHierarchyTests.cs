// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

public sealed class AgentMessageHierarchyTests
{
    private static readonly ImmutableArray<ContentPart> _sampleParts =
        [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)];

    [Fact]
    public void UserMessage_WhenConstructedWithValidArguments_ExposesSuppliedValues()
    {
        var id = new MessageId(Guid.NewGuid());
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var createdAt = DateTimeOffset.UnixEpoch;

        var message = new UserMessage(
            id,
            agentId,
            sessionId,
            null,
            branchId,
            runId,
            turnId,
            createdAt,
            MessageState.Complete,
            _sampleParts,
            ExtensionData.Empty);

        message.Id.ShouldBe(id);
        message.AgentId.ShouldBe(agentId);
        message.SessionId.ShouldBe(sessionId);
        message.ConversationId.ShouldBeNull();
        message.BranchId.ShouldBe(branchId);
        message.RunId.ShouldBe(runId);
        message.TurnId.ShouldBe(turnId);
        message.CreatedAt.ShouldBe(createdAt);
        message.State.ShouldBe(MessageState.Complete);
        message.Parts.ShouldBe(_sampleParts);
        message.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void SystemMessage_WhenConstructedWithNullRunAndTurn_Succeeds()
    {
        // System instructions may be recorded before any run exists.
        var message = new SystemMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            _sampleParts,
            ExtensionData.Empty);

        message.RunId.ShouldBeNull();
        message.TurnId.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(MessageFactories))]
    public void Constructor_WhenPartsIsDefault_ThrowsArgumentException(
        Func<ImmutableArray<ContentPart>, ExtensionData, AgentMessage> factory)
    {
        var exception = Should.Throw<ArgumentException>(() => factory(default, ExtensionData.Empty));

        exception.ParamName.ShouldBe("parts");
    }

    [Theory]
    [MemberData(nameof(MessageFactories))]
    public void Constructor_WhenExtensionsIsNull_ThrowsArgumentNullException(
        Func<ImmutableArray<ContentPart>, ExtensionData, AgentMessage> factory)
    {
        var exception = Should.Throw<ArgumentNullException>(() => factory(_sampleParts, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    public static TheoryData<Func<ImmutableArray<ContentPart>, ExtensionData, AgentMessage>> MessageFactories()
    {
        AgentMessage User(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new UserMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            extensions);

        AgentMessage System(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new SystemMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            extensions);

        AgentMessage Developer(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new DeveloperMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            extensions);

        AgentMessage Tool(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new ToolMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            extensions);

        AgentMessage Runtime(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new RuntimeMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            extensions);

        return new TheoryData<Func<ImmutableArray<ContentPart>, ExtensionData, AgentMessage>>
        {
            User, System, Developer, Tool, Runtime,
        };
    }
}
