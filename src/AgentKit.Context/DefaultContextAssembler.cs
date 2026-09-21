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
        var sourceCursor = evidence?.History.SourceCursor
            ?? new MessageCursor(
                request.AgentId,
                request.SessionId,
                conversationId: null,
                request.BranchId,
                new SessionVersion(0),
                new SessionSequence(0));
        var rawHistory = evidence?.History.Messages ?? request.History;
        var toolChoice = evidence?.Agent.ToolChoice ?? request.ToolChoice;
        var settings = evidence?.Agent.Settings ?? request.Settings;
        var fallbackTools = evidence?.Agent.Tools ?? request.Tools;

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
        SafeLog(() => ContextLog.Preparing(_logger, request.ModelRequestId, rawHistory.Length));

        var historyResult = await _services.History.PrepareAsync(
            new HistoryPreparationRequest(sourceCursor, rawHistory),
            cancellationToken).ConfigureAwait(false);
        if (historyResult is RejectedHistory rejectedHistory)
        {
            var failure = rejectedHistory.Failure.ToContextPreparationFailure();
            var outcome = failure.Kind switch
            {
                ContextPreparationFailureKind.EmptyHistory => "empty_history",
                ContextPreparationFailureKind.InvalidRolePartCombination => "invalid_role_part_combination",
                ContextPreparationFailureKind.BrokenToolCallCausality => "broken_tool_call_causality",
                _ => "history_preparation_failed",
            };
            SafeSetActivity(() => activity.SetFailed(outcome, failure.Kind.ToString()));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, failure.Kind));
            return new ContextPreparationFailed(failure);
        }

        var preparedHistory = ((PreparedHistory) historyResult).View;
        var excludedInstructionMessages = preparedHistory.Repairs.Count(static repair =>
            repair.Kind == HistoryRepairKind.ExcludedInstructionMessage);
        if (excludedInstructionMessages > 0)
        {
            SafeLog(() => ContextLog.ExcludedInstructionMessagesFromHistory(_logger, request.ModelRequestId, excludedInstructionMessages));
        }

        if (!preparedHistory.Repairs.IsEmpty)
        {
            var excludedIncompleteMessages = preparedHistory.Repairs.Count(static repair =>
                repair.Kind is HistoryRepairKind.ExcludedIncompleteMessage or HistoryRepairKind.ExcludedIncompleteAssistantContent);
            SafeLog(() => ContextLog.AppliedHistoryRepairs(
                _logger,
                request.ModelRequestId,
                preparedHistory.Repairs.Length,
                excludedIncompleteMessages,
                excludedInstructionMessages));
        }

        ImmutableArray<AgentMessage> instructions;
        if (evidence is not null)
        {
            var resolution = await _services.Instructions.ResolveAsync(
                new InstructionResolutionRequest(
                    evidence.Agent.InstructionSources,
                    request.RunId,
                    request.TurnId,
                    request.ModelRequestId),
                cancellationToken).ConfigureAwait(false);
            if (resolution is InstructionResolutionFailed instructionFailed)
            {
                const string outcome = "invalid_instruction_message";
                SafeSetActivity(() => activity.SetFailed(outcome, instructionFailed.Failure.Kind.ToString()));
                SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
                SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, instructionFailed.Failure.Kind));
                return new ContextPreparationFailed(instructionFailed.Failure);
            }

            instructions = ((InstructionResolutionResolved) resolution).Messages;
        }
        else
        {
            var instructionFailure = ValidateInstructions(request.Instructions);
            if (instructionFailure is not null)
            {
                const string outcome = "invalid_instruction_message";
                SafeSetActivity(() => activity.SetFailed(outcome, nameof(ContextPreparationFailureKind.InvalidInstructionMessage)));
                SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
                SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, ContextPreparationFailureKind.InvalidInstructionMessage));
                return new ContextPreparationFailed(instructionFailure);
            }

            instructions = request.Instructions;
        }

        var messages = instructions.AddRange(preparedHistory.Messages);

        // Validated over the combined messages, not just history: an instruction message could
        // itself carry a ToolCallPart/ToolResultPart, or reference/duplicate a call identity that
        // also appears in history, and neither half alone proves the composed request is coherent.
        var combinedFailure = HistoryMessageValidation.ValidateRolePartCombinations(messages)?.ToContextPreparationFailure()
            ?? HistoryMessageValidation.ValidateToolCallCausality(messages)?.ToContextPreparationFailure();
        if (combinedFailure is not null)
        {
            var outcome = combinedFailure.Kind == ContextPreparationFailureKind.InvalidRolePartCombination
                ? "invalid_role_part_combination"
                : "broken_tool_call_causality";
            SafeSetActivity(() => activity.SetFailed(outcome, combinedFailure.Kind.ToString()));
            SafeObserve(() => ContextMetrics.Preparations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            SafeLog(() => ContextLog.Rejected(_logger, request.ModelRequestId, combinedFailure.Kind));
            return new ContextPreparationFailed(combinedFailure);
        }

        var tools = await _services.Tools.ResolveToolsAsync(
            new ToolSnapshotRequest(catalog: null, fallbackTools),
            cancellationToken).ConfigureAwait(false);

        ContextManifest? manifest = null;
        if (request.Evidence is not null && _services.Contributors.Count > 0)
        {
            var (Manifest, Failure) = await RunContributorsAsync(request, preparedHistory, cancellationToken).ConfigureAwait(false);
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
        return new ContextReady(context, preparedHistory.Repairs);
    }

    private async Task<(ContextManifest? Manifest, ContextPreparationFailure? Failure)> RunContributorsAsync(
        ContextAssemblyRequest request,
        HistoryView preparedHistory,
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
            preparedHistory,
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
            preparedHistory.SourceCursor.Version,
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
    /// Validates that every instruction message is complete and carries system or developer
    /// authority for the reduced compatibility path that supplies flat instruction messages.
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
}
