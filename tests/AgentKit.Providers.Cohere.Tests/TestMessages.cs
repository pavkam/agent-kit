// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

/// <summary>
/// Builds minimal, valid <see cref="AgentMessage"/> instances for tests,
/// with fixed, arbitrary identity values that are not otherwise meaningful
/// to the behavior under test.
/// </summary>
internal static class TestMessages
{
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId Session = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly BranchId Branch = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly RunId Run = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static readonly TurnId Turn = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a <see cref="SystemMessage"/> containing a single text part.</summary>
    public static SystemMessage System(string text) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            runId: null,
            turnId: null,
            CreatedAt,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    /// <summary>Builds a <see cref="UserMessage"/> containing a single text part.</summary>
    public static UserMessage User(string text, MessageState state = MessageState.Complete) =>
        User(state, new TextPart(text, TextSemantics.Plain, ExtensionData.Empty));

    /// <summary>Builds a <see cref="UserMessage"/> containing the given parts.</summary>
    public static UserMessage User(params ContentPart[] parts) => User(MessageState.Complete, parts);

    /// <summary>Builds a <see cref="UserMessage"/> containing the given parts with an explicit state.</summary>
    public static UserMessage User(MessageState state, params ContentPart[] parts) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            Run,
            Turn,
            CreatedAt,
            state,
            [.. parts], ExtensionData.Empty);

    /// <summary>Builds a <see cref="RuntimeMessage"/> containing a single text part.</summary>
    public static RuntimeMessage Runtime(string text) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            Run,
            Turn,
            CreatedAt,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    /// <summary>Builds an <see cref="AssistantMessage"/> containing the given parts.</summary>
    public static AssistantMessage Assistant(params ContentPart[] parts) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            Run,
            Turn,
            CreatedAt,
            MessageState.Complete,
            [.. parts],
            new AssistantResponseMetadata(
                new ModelRequestId(Guid.NewGuid()),
                new ProviderResponseIdentity(
                    CohereProviderDefaults.ProviderId,
                    upstreamProviderId: null,
                    CohereProviderDefaults.ApiFamily,
                    new ModelId("command-a-plus-05-2026"),
                    new ModelId("command-a-plus-05-2026"),
                    deploymentId: null,
                    requestId: null,
                    responseId: null),
                NormalizedStopReason.ToolUse,
                rawStopReason: "TOOL_CALL",
                ModelUsage.NotReported,
                ExtensionData.Empty),
            ExtensionData.Empty);

    /// <summary>Builds a <see cref="ToolMessage"/> containing the given parts.</summary>
    public static ToolMessage Tool(params ContentPart[] parts) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            Run,
            Turn,
            CreatedAt,
            MessageState.Complete,
            [.. parts], ExtensionData.Empty);
}
