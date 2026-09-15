// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies UserMessage behavior and contracts.</summary>
public sealed class UserMessageTests
{
    private static readonly Guid _messageGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _agentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _sessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _branchGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    [Fact]
    public void UserMessage_Equality_WhenSameValues_InstancesAreEqual() => new UserMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty).ShouldBe(new UserMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));
    private static MessageId MessageId => new(_messageGuid);
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static ImmutableArray<ContentPart> Parts(string text = "hi") => [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)];
    private static readonly ImmutableArray<ContentPart> _sampleParts = [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)];
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
        var message = new UserMessage(id, agentId, sessionId, null, branchId, runId, turnId, createdAt, MessageState.Complete, _sampleParts, ExtensionData.Empty);
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

    private static UserMessage CreateUserMessage(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, MessageState.Complete, parts, extensions);
    [Fact]
    public void Constructor_WhenPartsIsDefault_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => CreateUserMessage(default, ExtensionData.Empty)).ParamName.ShouldBe("parts");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => CreateUserMessage(_sampleParts, null!)).ParamName.ShouldBe("extensions");

    [Fact]
    public void Constructor_WhenPartsContainNull_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => CreateUserMessage([null!], ExtensionData.Empty)).ParamName.ShouldBe("parts");

    [Fact]
    public void With_WhenPartsIsDefault_ThrowsArgumentException()
    {
        var message = CreateUserMessage(_sampleParts, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => message with { Parts = default });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenPartsContainNull_ThrowsArgumentException()
    {
        var message = CreateUserMessage(_sampleParts, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => message with { Parts = [null!] });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var message = CreateUserMessage(_sampleParts, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => message with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }
}
