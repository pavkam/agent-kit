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
internal sealed class DefaultContextAssembler: IContextAssembler
{
    private readonly ContextAssemblerServices _services;
    private readonly ILogger<DefaultContextAssembler> _logger;
    private readonly HashSet<ContextSourceKey> _oncePerRunContributorsExecuted = [];

    /// <summary>Initializes an assembler with its keyed collaborators.</summary>
    /// <param name="services">The compiled contributor and budget services for this assembler key.</param>
    /// <param name="timeProvider">The clock used for contributor freshness evidence.</param>
    /// <param name="logger">The logger that receives safe context-preparation diagnostics.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public DefaultContextAssembler(
        ContextAssemblerServices services,
        TimeProvider timeProvider,
        ILogger<DefaultContextAssembler> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _services = services;
        _ = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ContextAssemblyResult> AssembleAsync(
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

        var instructionFailure = ValidateInstructions(instructions);
        if (instructionFailure is not null)
        {
            const string outcome = "invalid_instruction_message";
            SafeSetActivity(() => activity.SetFailed(outcome, nameof(ContextPreparationFailureKind.InvalidInstructionMessage)));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, ContextPreparationFailureKind.InvalidInstructionMessage));
            return new ContextPreparationFailed(instructionFailure);
        }

        if (repairedHistory.IsEmpty)
        {
            const string outcome = "empty_history";
            SafeSetActivity(() => activity.SetFailed(outcome, nameof(ContextPreparationFailureKind.EmptyHistory)));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, ContextPreparationFailureKind.EmptyHistory));
            return new ContextPreparationFailed(
                new ContextPreparationFailure(
                    ContextPreparationFailureKind.EmptyHistory,
                    "The eligible conversation history contains no complete messages to send.",
                    ExtensionData.Empty));
        }

        var messages = instructions.AddRange(repairedHistory);

        // Validated over the combined messages, not just history: an instruction message could
        // itself carry a ToolCallPart/ToolResultPart, or reference/duplicate a call identity that
        // also appears in history, and neither half alone proves the composed request is coherent.
        var structuralFailure = ValidateRolePartCombinations(messages) ?? ValidateToolCallCausality(messages);
        if (structuralFailure is not null)
        {
            var outcome = structuralFailure.Kind == ContextPreparationFailureKind.InvalidRolePartCombination
                ? "invalid_role_part_combination"
                : "broken_tool_call_causality";
            SafeSetActivity(() => activity.SetFailed(outcome, structuralFailure.Kind.ToString()));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, structuralFailure.Kind));
            return new ContextPreparationFailed(structuralFailure);
        }

        ContextManifest? manifest = null;
        if (request.Evidence is not null && _services.Contributors.Count > 0)
        {
            var (Manifest, Failure) = await RunContributorsAsync(request, cancellationToken).ConfigureAwait(false);
            if (Failure is { } contributorFailure)
            {
                const string outcome = "contributor_failure";
                SafeSetActivity(() => activity.SetFailed(outcome, contributorFailure.Kind.ToString()));
                SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
                SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, contributorFailure.Kind));
                return new ContextPreparationFailed(contributorFailure);
            }

            manifest = Manifest;
        }

        var context = new LlmRequestContext(
            request.ModelRequestId,
            request.Model,
            messages,
            tools,
            toolChoice,
            settings,
            request.Extensions)
        {
            Manifest = manifest,
        };

        SafeSetActivity(() => activity.SetSuccessful("ready"));
        SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "ready")));
        SafeLog(() => ContextLog.Prepared(_logger, request.ModelRequestId, messages.Length));
        return new ContextReady(context, repairs);
    }

    private async Task<(ContextManifest? Manifest, ContextPreparationFailure? Failure)> RunContributorsAsync(
        ContextAssemblyRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request.Evidence is not null, "Contributors run only when assembly evidence is present.");
        var evidence = request.Evidence;
        var contributionRequest = new ContextContributionRequest(
            evidence.Agent,
            request.SessionId,
            evidence.History.SourceCursor.ConversationId,
            evidence.Identity,
            request.RunId,
            request.TurnId,
            request.ModelRequestId,
            request.Model,
            evidence.History,
            evidence.Authorization,
            evidence.Configuration);

        var candidates = ImmutableArray.CreateBuilder<ContextCandidate>();
        foreach (var registered in _services.Contributors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registration = registered.Registration;
            if (registration.Frequency == ContextEvaluationFrequency.OncePerRun
                && !_oncePerRunContributorsExecuted.Add(registration.ContributorId))
            {
                continue;
            }

            ContextContribution contribution;
            try
            {
                contribution = await registered.Contributor.ContributeAsync(contributionRequest, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!registration.Required)
                {
                    continue;
                }

                return (null, new ContextPreparationFailure(
                    ContextPreparationFailureKind.Unknown,
                    "A required context contributor failed before returning a bounded contribution.",
                    ExtensionData.Empty));
            }

            foreach (var candidate in contribution.Candidates)
            {
                var trustViolation = ContextTrustRules.ValidateCandidate(candidate);
                if (trustViolation is not null)
                {
                    return (null, new ContextPreparationFailure(ContextPreparationFailureKind.Unknown, trustViolation, ExtensionData.Empty));
                }

                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            return (null, null);
        }

        var maxContextTokens = request.Model.Limits.MaxContextTokens ?? 128_000;
        var options = _services.Options;
        var budget = new ContextBudget(
            maxContextTokens,
            options.ReservedOutputTokens,
            options.ProviderOverheadTokens,
            options.EstimationSafetyMargin);
        var budgetRequest = new ContextBudgetRequest(
            budget,
            candidates.ToImmutable(),
            options.OverflowBehavior);
        var plan = await _services.Budgets.AllocateAsync(budgetRequest, cancellationToken).ConfigureAwait(false);
        if (plan.MandatoryOverflow)
        {
            return (null, new ContextPreparationFailure(
                ContextPreparationFailureKind.Unknown,
                "Mandatory context content exceeded the available token budget.",
                ExtensionData.Empty));
        }

        var entries = ImmutableArray.CreateBuilder<ContextManifestEntry>();
        foreach (var candidate in plan.Selected)
        {
            entries.Add(new ContextManifestEntry(
                candidate.Source,
                ContextManifestDisposition.Included,
                candidate.Cost,
                "Selected within the available context budget.",
                []));
        }

        foreach (var candidate in plan.Omitted)
        {
            entries.Add(new ContextManifestEntry(
                candidate.Source,
                ContextManifestDisposition.Omitted,
                candidate.Cost,
                "Omitted because optional content did not fit within the available context budget.",
                []));
        }

        var manifest = new ContextManifest(
            request.ModelRequestId,
            evidence.Agent.Revision,
            evidence.History.SourceCursor.Version,
            evidence.Configuration.Version,
            new ContextContributorCatalogVersion(_services.Contributors.Count),
            request.Model,
            entries.ToImmutable(),
            plan.EstimatedTotal);
        return (manifest, null);
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
    /// Validates that every instruction message is complete and carries system or developer
    /// authority. Unlike history, an instruction message is never repaired or silently excluded:
    /// instructions enter a request exclusively through this explicit set, so a caller that places an
    /// incomplete, user, assistant, or tool message in it has produced an incoherent request that
    /// assembly must reject rather than send unrepaired and unvalidated.
    /// </summary>
    private static ContextPreparationFailure? ValidateInstructions(ImmutableArray<AgentMessage> instructions)
    {
        Debug.Assert(!instructions.IsDefault, "The request and evidence contracts guarantee an initialized instruction set.");
        foreach (var message in instructions)
        {
            if (message.State != MessageState.Complete)
            {
                return new ContextPreparationFailure(
                    ContextPreparationFailureKind.InvalidInstructionMessage,
                    $"An instruction message's state is {message.State}, but only complete messages may be sent to a provider.",
                    ExtensionData.Empty);
            }

            if (message is not (SystemMessage or DeveloperMessage))
            {
                return new ContextPreparationFailure(
                    ContextPreparationFailureKind.InvalidInstructionMessage,
                    "An instruction message is not a system or developer message; only those roles may carry instruction authority.",
                    ExtensionData.Empty);
            }
        }

        return null;
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
