// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>
/// The default <see cref="IContextAssembler"/>: repairs already-loaded
/// history to complete messages only, validates that every tool call and
/// tool result in the repaired history sit in the right role and causally
/// match, and combines instructions, history, tools, and settings into one
/// <see cref="LlmRequestContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation performs no I/O, retrieval, or compaction of its
/// own; it operates entirely over the <see cref="ContextAssemblyRequest.History"/>
/// and <see cref="ContextAssemblyRequest.Instructions"/> the caller
/// supplies. See <see cref="IContextAssembler"/> for the reduced-scope
/// rationale shared by every implementation of this contract.
/// </para>
/// <para>
/// History repair keeps only messages whose <see cref="AgentMessage.State"/>
/// is <see cref="MessageState.Complete"/> and excludes any
/// <see cref="SystemMessage"/> or <see cref="DeveloperMessage"/> found in
/// history: an incomplete, suspended, or interrupted message is never sent to
/// a provider as conversational input, and stored history never carries
/// instruction authority. Every exclusion is reported as a
/// <see cref="HistoryRepair"/> on <see cref="ContextReady.Repairs"/>, keyed by
/// the excluded message's identity, so a repair is attributable rather than
/// silent; the durable source history is never modified.
/// </para>
/// <para>
/// Structural validation then requires a <see cref="ToolCallPart"/> to appear
/// only in an <see cref="AssistantMessage"/> and a <see cref="ToolResultPart"/>
/// only in a <see cref="ToolMessage"/>, failing with
/// <see cref="ContextPreparationFailureKind.InvalidRolePartCombination"/>
/// otherwise. Tool-causality validation requires the surviving history to
/// carry exactly one <see cref="ToolResultPart"/> for every
/// <see cref="ToolCallPart"/>, no result before or without its call, and no
/// repeated call identity; either violation fails assembly with
/// <see cref="ContextPreparationFailureKind.BrokenToolCallCausality"/>
/// rather than sending a provider a request it cannot interpret.
/// </para>
/// </remarks>
public sealed class DefaultContextAssembler: IContextAssembler
{
    private readonly ILogger<DefaultContextAssembler> _logger;

    /// <summary>
    /// Initializes an assembler that emits no logs unless constructed by dependency injection.
    /// </summary>
    /// <remarks>
    /// This compatibility constructor uses Microsoft's null logger. Applications
    /// should normally resolve the assembler after calling <c>AddAgentContext</c>.
    /// </remarks>
    public DefaultContextAssembler()
        : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultContextAssembler>.Instance)
    {
    }

    /// <summary>Initializes an assembler with its type-specific structured logger.</summary>
    /// <param name="logger">The logger that receives safe context-preparation diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    public DefaultContextAssembler(ILogger<DefaultContextAssembler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<ContextAssemblyResult> AssembleAsync(
        ContextAssemblyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var evidence = request.Evidence;
        var agentId = evidence?.Agent.Id ?? request.AgentId;
        var sessionId = evidence?.History.SourceCursor.SessionId ?? request.SessionId;
        var history = evidence?.History.Messages ?? request.History;
        var instructions = evidence?.Agent.Instructions ?? request.Instructions;
        var tools = evidence?.Agent.Tools ?? request.Tools;
        var toolChoice = evidence?.Agent.ToolChoice ?? request.ToolChoice;
        var settings = evidence?.Agent.Settings ?? request.Settings;

        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.ContextPrepare,
            ActivityKind.Internal,
            new KeyValuePair<string, object?>[]
            {
                new(AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ContextPrepare),
                new(AgentKitTagNames.AgentId, agentId.ToString()),
                new(AgentKitTagNames.SessionId, sessionId.ToString()),
                new(AgentKitTagNames.RunId, request.RunId.ToString()),
                new(AgentKitTagNames.TurnId, request.TurnId.ToString()),
                new(AgentKitTagNames.ModelRequestId, request.ModelRequestId.ToString()),
                new(AgentKitTagNames.RequestModel, request.Model.ModelId.ToString()),
            });
        var activity = activityScope.Activity;
        SafeLog(() => ContextLog.Preparing(_logger, request.ModelRequestId, history.Length));

        var repairedHistory = RepairHistory(history, out var repairs, out var excludedIncompleteMessages, out var excludedInstructionMessages);
        if (excludedInstructionMessages > 0)
        {
            SafeLog(() => ContextLog.ExcludedInstructionMessagesFromHistory(_logger, request.ModelRequestId, excludedInstructionMessages));
        }

        if (!repairs.IsEmpty)
        {
            SafeLog(() => ContextLog.AppliedHistoryRepairs(
                _logger, request.ModelRequestId, repairs.Length, excludedIncompleteMessages, excludedInstructionMessages));
        }

        if (repairedHistory.IsEmpty)
        {
            const string outcome = "empty_history";
            SafeSetActivity(() => activity.SetFailed(outcome, nameof(ContextPreparationFailureKind.EmptyHistory)));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, ContextPreparationFailureKind.EmptyHistory));
            return Task.FromResult<ContextAssemblyResult>(
                new ContextPreparationFailed(
                    new ContextPreparationFailure(
                        ContextPreparationFailureKind.EmptyHistory,
                        "The eligible conversation history contains no complete messages to send.",
                        ExtensionData.Empty)));
        }

        var structuralFailure = ValidateRolePartCombinations(repairedHistory) ?? ValidateToolCallCausality(repairedHistory);
        if (structuralFailure is not null)
        {
            var outcome = structuralFailure.Kind == ContextPreparationFailureKind.InvalidRolePartCombination
                ? "invalid_role_part_combination"
                : "broken_tool_call_causality";
            SafeSetActivity(() => activity.SetFailed(outcome, structuralFailure.Kind.ToString()));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, structuralFailure.Kind));
            return Task.FromResult<ContextAssemblyResult>(new ContextPreparationFailed(structuralFailure));
        }

        var messages = instructions.AddRange(repairedHistory);

        var context = new LlmRequestContext(
            request.ModelRequestId,
            request.Model,
            messages,
            tools,
            toolChoice,
            settings,
            request.Extensions);

        SafeSetActivity(() => activity.SetSuccessful("ready"));
        SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "ready")));
        SafeLog(() => ContextLog.Prepared(_logger, request.ModelRequestId, messages.Length));
        return Task.FromResult<ContextAssemblyResult>(new ContextReady(context, repairs));
    }

    /// <summary>Runs one activity mutation, containing a hostile diagnostics listener so it cannot alter the returned decision.</summary>
    private static void SafeSetActivity(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a listener failure must never alter the assembled result.
        }
    }

    /// <summary>Runs one log call, containing a hostile logging provider so it cannot alter the returned decision.</summary>
    private static void SafeLog(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a logging-provider failure must never alter the assembled result.
        }
    }

    /// <summary>Runs one metrics call, containing a hostile measurement callback so it cannot alter the returned decision.</summary>
    private static void SafeObserve(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a meter-listener failure must never alter the assembled result.
        }
    }

    /// <summary>
    /// Retains only complete messages and excludes any system or developer message found in history:
    /// instruction authority enters a request exclusively through the explicit instruction set, so a
    /// stored or imported history can never promote content to system precedence. Every exclusion
    /// produces one <see cref="HistoryRepair"/> attributed to the excluded message, in source order.
    /// </summary>
    private static ImmutableArray<AgentMessage> RepairHistory(
        ImmutableArray<AgentMessage> history,
        out ImmutableArray<HistoryRepair> repairs,
        out int excludedIncompleteMessages,
        out int excludedInstructionMessages)
    {
        Debug.Assert(!history.IsDefault, "The request and evidence contracts guarantee an initialized history.");
        excludedIncompleteMessages = 0;
        excludedInstructionMessages = 0;
        var builder = ImmutableArray.CreateBuilder<AgentMessage>(history.Length);
        var repairBuilder = ImmutableArray.CreateBuilder<HistoryRepair>();
        foreach (var message in history)
        {
            if (message.State != MessageState.Complete)
            {
                excludedIncompleteMessages++;
                repairBuilder.Add(new HistoryRepair(
                    [message.Id],
                    message is AssistantMessage
                        ? HistoryRepairKind.ExcludedIncompleteAssistantContent
                        : HistoryRepairKind.ExcludedIncompleteMessage,
                    $"Excluded a message whose state is {message.State} because only complete messages are sent to a provider.",
                    ExtensionData.Empty));
                continue;
            }

            if (message is SystemMessage or DeveloperMessage)
            {
                excludedInstructionMessages++;
                repairBuilder.Add(new HistoryRepair(
                    [message.Id],
                    HistoryRepairKind.ExcludedInstructionMessage,
                    "Excluded a system or developer message found in history because history never carries instruction authority.",
                    ExtensionData.Empty));
                continue;
            }

            builder.Add(message);
        }

        repairs = repairBuilder.ToImmutable();
        return builder.ToImmutable();
    }

    /// <summary>
    /// Validates that each tool part sits in the only role allowed to carry it: a <see cref="ToolCallPart"/>
    /// in an <see cref="AssistantMessage"/> and a <see cref="ToolResultPart"/> in a <see cref="ToolMessage"/>.
    /// Because each half is confined to its own role, a call and its result can never share one message.
    /// </summary>
    private static ContextPreparationFailure? ValidateRolePartCombinations(ImmutableArray<AgentMessage> repairedHistory)
    {
        Debug.Assert(!repairedHistory.IsDefault, "RepairHistory always returns an initialized array.");
        foreach (var message in repairedHistory)
        {
            foreach (var part in message.Parts)
            {
                var violation = part switch
                {
                    ToolCallPart when message is not AssistantMessage =>
                        "History carries a tool call in a message whose role cannot request tools; only assistant messages may.",
                    ToolResultPart when message is not ToolMessage =>
                        "History carries a tool result in a message whose role cannot report results; only tool messages may.",
                    _ => null,
                };
                if (violation is not null)
                {
                    return new ContextPreparationFailure(
                        ContextPreparationFailureKind.InvalidRolePartCombination, violation, ExtensionData.Empty);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates tool-call causality in order: every call identity is unique, every result references a
    /// call in an earlier message, and every call receives exactly one terminal result.
    /// </summary>
    private static ContextPreparationFailure? ValidateToolCallCausality(ImmutableArray<AgentMessage> repairedHistory)
    {
        Debug.Assert(!repairedHistory.IsDefault, "RepairHistory always returns an initialized array.");
        var pendingCalls = new HashSet<ToolCallId>();
        var seenCalls = new HashSet<ToolCallId>();
        string? violation = null;

        foreach (var message in repairedHistory)
        {
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case ToolCallPart toolCall when !seenCalls.Add(toolCall.CallId):
                        violation = "History requested the same tool call identity more than once.";
                        break;
                    case ToolCallPart toolCall:
                        _ = pendingCalls.Add(toolCall.CallId);
                        break;
                    case ToolResultPart toolResult when !pendingCalls.Remove(toolResult.CallId):
                        violation = seenCalls.Contains(toolResult.CallId)
                            ? "History carries more than one terminal result for one tool call."
                            : "History carries a tool result that precedes, or has no, matching tool call.";
                        break;
                    default:
                        break;
                }

                if (violation is not null)
                {
                    return new ContextPreparationFailure(
                        ContextPreparationFailureKind.BrokenToolCallCausality, violation, ExtensionData.Empty);
                }
            }
        }

        return pendingCalls.Count == 0
            ? null
            : new ContextPreparationFailure(
                ContextPreparationFailureKind.BrokenToolCallCausality,
                "Every tool call in history must have exactly one matching terminal result.",
                ExtensionData.Empty);
    }
}
