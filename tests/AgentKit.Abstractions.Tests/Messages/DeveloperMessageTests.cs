// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies DeveloperMessage behavior and contracts.</summary>
public sealed class DeveloperMessageTests
{
    private static readonly Guid _messageGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _agentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _sessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _branchGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    [Fact]
    public void DeveloperMessage_Equality_WhenSameValues_InstancesAreEqual() => new DeveloperMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty).ShouldBe(new DeveloperMessage(MessageId, AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, Parts(), ExtensionData.Empty));
    private static MessageId MessageId => new(_messageGuid);
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static ImmutableArray<ContentPart> Parts(string text = "hi") => [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)];
    private static readonly ImmutableArray<ContentPart> _sampleParts = [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)];
    private static DeveloperMessage CreateDeveloperMessage(ImmutableArray<ContentPart> parts, ExtensionData extensions) => new(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, parts, extensions);
    [Fact]
    public void Constructor_WhenPartsIsDefault_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => CreateDeveloperMessage(default, ExtensionData.Empty)).ParamName.ShouldBe("parts");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => CreateDeveloperMessage(_sampleParts, null!)).ParamName.ShouldBe("extensions");
}
