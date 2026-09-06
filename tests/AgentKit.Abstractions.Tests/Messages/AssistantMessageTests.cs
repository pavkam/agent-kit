// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

public sealed class AssistantMessageTests
{
    private static readonly ImmutableArray<ContentPart> _sampleParts =
        [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)];

    private static AssistantResponseMetadata CreateResponse() => new(
        new ModelRequestId(Guid.NewGuid()),
        new ProviderResponseIdentity(
            new ProviderId("openai"),
            null,
            new ApiFamilyId("chat-completions"),
            new ModelId("gpt"),
            new ModelId("gpt"),
            null,
            null,
            null),
        NormalizedStopReason.Completed,
        "stop",
        ModelUsage.Empty,
        ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AssistantMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            _sampleParts,
            null!,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesResponse()
    {
        var response = CreateResponse();

        var message = new AssistantMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            _sampleParts,
            response,
            ExtensionData.Empty);

        message.Response.ShouldBe(response);
    }
}
