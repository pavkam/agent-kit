// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Builds the loop's <see cref="AgentRunOutcome"/> values from the closed canonical family
/// (<see cref="RunSucceeded"/>, <see cref="RunIdle"/>, <see cref="RunCancelled"/>, <see cref="RunLimitReached"/>,
/// <see cref="RunPolicyHalted"/>, <see cref="RunFailed"/>), so every settlement site constructs one of these
/// through a single reviewable mapping instead of repeating <see cref="AgentError"/> boilerplate.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RunLimitFailure"/> is scoped to genuine budget-authority evidence: its <see cref="RunLimitFailure.Limit"/>
/// is a <see cref="BudgetLimitFailure"/>, which always names a real <see cref="BudgetScopeId"/> a reservation was
/// evaluated against. A loop-configured ceiling the budget authority never reserved against — the request's own
/// <see cref="AgentLoopRunRequest.MaxTurns"/>, or a provider's own generation-length ceiling — has no such scope, and
/// fabricating one would misrepresent the cause. Those two cases settle as <see cref="RunPolicyHalted"/> and
/// <see cref="RunFailed"/> respectively instead; see <see cref="TurnLimitReached"/> and
/// <see cref="OutputLengthLimitReached"/> for the rationale specific to each.
/// </para>
/// <para>
/// Every mapping here is <see cref="SideEffectCertainty.NotApplicable"/> on the <see cref="AgentError"/> itself:
/// none of these outcomes report on a specific external, potentially-repeatable effect the way a tool invocation
/// does, so there is no effect certainty to report independently of the outcome itself.
/// </para>
/// </remarks>
internal static class RunOutcomes
{
    private static readonly ErrorOrigin _origin = new("agentkit.loop");

    /// <summary>Builds the outcome for a run that reached a final assistant message with no further tool calls.</summary>
    /// <returns>The canonical successful outcome; the committed message and any validated output live on the loop's own result envelope.</returns>
    public static AgentRunOutcome Completed() => new RunSucceeded();

    /// <summary>Builds the outcome for a run that completed at an idle boundary with no pending work.</summary>
    /// <returns>The canonical idle outcome.</returns>
    public static AgentRunOutcome Idle() => new RunIdle();

    /// <summary>Builds the outcome for a run cancelled before or after committing truthful partial output.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <returns>A <see cref="RunCancelled"/> outcome wrapping a normalized <see cref="AgentErrorCodes.Cancelled"/> error.</returns>
    public static AgentRunOutcome Cancelled(string safeMessage) =>
        new RunCancelled(new CancellationReason(Error(AgentErrorCodes.Cancelled, safeMessage)));

    /// <summary>
    /// Builds the outcome for a run halted because it reached <see cref="AgentLoopRunRequest.MaxTurns"/> while tool
    /// calls were still pending resolution.
    /// </summary>
    /// <param name="maxTurns">The configured maximum number of turns that was reached.</param>
    /// <returns>
    /// A <see cref="RunPolicyHalted"/> outcome: the caller's own configured turn ceiling, not a budget-authority
    /// reservation, stopped the run, which is exactly what <see cref="PolicyHalt"/> exists to describe.
    /// </returns>
    public static AgentRunOutcome TurnLimitReached(int maxTurns) =>
        new RunPolicyHalted(new PolicyHalt(Error(
            AgentErrorCodes.RequestLimit,
            $"The run reached its {maxTurns}-turn limit.")));

    /// <summary>The stable enforcement boundary named on every <see cref="RunLimitFailure"/> the loop's own budget accounting produces.</summary>
    private static readonly ComponentId _budgetEnforcementBoundary = new("agentkit.loop.budget");

    /// <summary>Builds the outcome for a refused budget reservation the loop's own accounting reported.</summary>
    /// <param name="exhaustion">The exhaustion the reservation attempt reported.</param>
    /// <param name="hasPartialOutput">Whether truthful partial output already exists.</param>
    /// <returns>
    /// A <see cref="RunLimitReached"/> outcome when the exhaustion carries full budget evidence; otherwise a
    /// <see cref="RunFailed"/> outcome, since there is nothing to build truthful limit evidence from.
    /// </returns>
    public static AgentRunOutcome BudgetExhausted(BudgetExhaustion exhaustion, bool hasPartialOutput) =>
        exhaustion.Failure is { } failure
            ? BudgetExhausted(failure, _budgetEnforcementBoundary, hasPartialOutput, exhaustion.SideEffectCertainty)
            : BudgetHeldOrUnsupported(exhaustion.Dimension, exhaustion.SafeMessage);

    /// <summary>Builds the outcome for a run halted because a rejected budget reservation carried full limit evidence.</summary>
    /// <param name="failure">The typed limit failure the budget authority returned for a rejected reservation.</param>
    /// <param name="enforcementBoundary">The component that enforced the limit.</param>
    /// <param name="hasPartialOutput">Whether truthful partial output already exists.</param>
    /// <param name="sideEffectCertainty">What is known about the relevant external effect the refused attempt would have made.</param>
    /// <returns>A <see cref="RunLimitReached"/> outcome carrying the exact budget evidence.</returns>
    public static AgentRunOutcome BudgetExhausted(
        BudgetLimitFailure failure, ComponentId enforcementBoundary, bool hasPartialOutput, SideEffectCertainty sideEffectCertainty) =>
        new RunLimitReached(new RunLimitFailure(failure, enforcementBoundary, hasPartialOutput, sideEffectCertainty));

    /// <summary>
    /// Builds the outcome for a run halted by a budget-dimension exhaustion that carries no full
    /// <see cref="BudgetLimitFailure"/> evidence: a captured overrun hold, or an unsupported reservation result.
    /// </summary>
    /// <param name="dimension">The dimension that ran out.</param>
    /// <param name="safeMessage">A content-safe description of the exhaustion.</param>
    /// <returns>
    /// A <see cref="RunFailed"/> outcome: without a rejected reservation's full evidence there is nothing to build
    /// a truthful <see cref="RunLimitFailure"/> from, so this is reported as a failure rather than a fabricated limit.
    /// </returns>
    public static AgentRunOutcome BudgetHeldOrUnsupported(BudgetDimension dimension, string safeMessage) =>
        new RunFailed(new RunFailure(Error(
            DimensionErrorCode(dimension), $"The {dimension.Value} budget is exhausted: {safeMessage}")));

    /// <summary>Builds the outcome for a run halted because terminal output was rejected or its configuration was invalid.</summary>
    /// <param name="rejection">The exhausted candidate or configuration rejection result.</param>
    /// <returns>
    /// A <see cref="RunPolicyHalted"/> outcome: the selected output contract is a policy the run's definition
    /// chose, and a rejection is that policy declining the candidate, not a provider or framework failure.
    /// </returns>
    public static AgentRunOutcome OutputRejected(OutputProcessingResult rejection) => rejection switch
    {
        OutputRejected candidate => new RunPolicyHalted(new PolicyHalt(Error(
            AgentErrorCodes.OutputValidationFailed, candidate.Failure.SafeMessage))),
        OutputConfigurationRejected configuration => new RunPolicyHalted(new PolicyHalt(Error(
            AgentErrorCodes.InvalidConfiguration, configuration.Failure.SafeMessage))),
        _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "Output processing results are closed to OutputRejected and OutputConfigurationRejected."),
    };

    /// <summary>
    /// Builds the outcome for a run stopped because the model's terminal response reported
    /// <see cref="NormalizedStopReason.Length"/>.
    /// </summary>
    /// <param name="modelRequestId">The identity of the model request whose output was truncated.</param>
    /// <param name="hasPartialOutput">Whether truncated partial output was committed as an interrupted message.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation that contains no model output.</param>
    /// <returns>
    /// A <see cref="RunFailed"/> outcome: this is the model provider's own generation-length ceiling, never a
    /// budget-authority reservation the loop could name a <see cref="BudgetScopeId"/> for, so it cannot honestly
    /// become <see cref="RunLimitReached"/> either.
    /// </returns>
    public static AgentRunOutcome OutputLengthLimitReached(ModelRequestId modelRequestId, bool hasPartialOutput, string safeMessage) =>
        new RunFailed(new RunFailure(Error(
            AgentErrorCodes.TokenLimit, safeMessage, diagnostics: Diagnostics(("modelRequestId", modelRequestId.Value.ToString()), ("hasPartialOutput", hasPartialOutput.ToString())))));

    /// <summary>Builds the outcome for a run stopped because fresh authorization could not be captured.</summary>
    /// <param name="safeReason">A human-readable, non-sensitive explanation.</param>
    /// <returns>A <see cref="RunFailed"/> outcome wrapping an <see cref="AgentErrorCodes.AuthorizationDenied"/> error.</returns>
    public static AgentRunOutcome AuthorizationUnavailable(string safeReason) =>
        new RunFailed(new RunFailure(Error(AgentErrorCodes.AuthorizationDenied, safeReason)));

    /// <summary>Builds the outcome for a run halted because context assembly failed before any provider I/O.</summary>
    /// <param name="failure">Why context assembly failed.</param>
    /// <returns>A <see cref="RunFailed"/> outcome preserving the failure's kind and safe message.</returns>
    public static AgentRunOutcome ContextPreparationFailed(ContextPreparationFailure failure) =>
        new RunFailed(new RunFailure(Error(
            ContextPreparationErrorCode(failure.Kind), failure.SafeMessage,
            diagnostics: Diagnostics(("contextPreparationFailureKind", failure.Kind.ToString())))));

    /// <summary>Builds the outcome for a run ended because no usable model could be chosen for it.</summary>
    /// <param name="safeReason">A redacted, human-readable explanation of why no model could be used.</param>
    /// <param name="diagnostics">Per-candidate selection outcomes, when selection ran.</param>
    /// <returns>A <see cref="RunFailed"/> outcome wrapping an <see cref="AgentErrorCodes.IncompatibleModel"/> error.</returns>
    public static AgentRunOutcome ModelSelectionFailed(string safeReason, ImmutableArray<ModelSelectionDiagnostic> diagnostics) =>
        new RunFailed(new RunFailure(Error(
            AgentErrorCodes.IncompatibleModel, safeReason,
            diagnostics: Diagnostics(("candidateCount", diagnostics.Length.ToString(CultureInfo.InvariantCulture))))));

    /// <summary>Builds the outcome for a run halted because a session read or append operation did not succeed.</summary>
    /// <param name="safeMessage">A safe, non-sensitive description of the failure.</param>
    /// <returns>A <see cref="RunFailed"/> outcome wrapping an unclassified error, matching the untyped evidence this call site has always carried.</returns>
    public static AgentRunOutcome SessionOperationFailed(string safeMessage) =>
        new RunFailed(new RunFailure(Error(AgentErrorCodes.Unknown, safeMessage)));

    /// <summary>Builds the outcome for a run that cannot continue because its captured lifecycle evidence is internally inconsistent.</summary>
    /// <param name="safeMessage">Bounded diagnostic text containing no prompt, model, or tool content.</param>
    /// <returns>A <see cref="RunFailed"/> outcome wrapping the exact-matching <see cref="AgentErrorCodes.InvalidState"/> error.</returns>
    public static AgentRunOutcome InvalidState(string safeMessage) =>
        new RunFailed(new RunFailure(Error(AgentErrorCodes.InvalidState, safeMessage)));

    /// <summary>Builds the outcome for a run stopped because a model attempt failed unexpectedly.</summary>
    /// <param name="failure">The normalized provider failure that stopped the run.</param>
    /// <returns>
    /// A <see cref="RunCancelled"/> outcome when the provider itself normalized the failure as a cancellation;
    /// otherwise a <see cref="RunFailed"/> outcome preserving the provider's kind, code, and retry evidence.
    /// </returns>
    public static AgentRunOutcome ModelAttemptFailed(ProviderFailure failure)
    {
        if (failure.Kind == ProviderFailureKind.Cancellation)
        {
            return Cancelled(failure.SafeMessage);
        }

        var error = new AgentError(
            ProviderFailureErrorCode(failure.Kind),
            failure.SafeMessage,
            isRetryable: false,
            SideEffectCertainty.NotApplicable,
            _origin,
            failure.ProviderCode,
            operationId: null,
            externalRequestId: null,
            failure.RetryAfter,
            Diagnostics(
                ("providerId", failure.ProviderId.Value),
                ("providerFailureKind", failure.Kind.ToString()),
                ("statusCode", failure.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)));
        return new RunFailed(new RunFailure(error));
    }

    /// <summary>Builds a normalized <see cref="AgentError"/> shared by every mapping in this type.</summary>
    /// <param name="code">The nondefault stable or custom error code.</param>
    /// <param name="safeMessage">A nonblank message already classified as safe for its intended surface.</param>
    /// <param name="diagnostics">Additional safe structured diagnostic evidence, or <see langword="null"/> for none.</param>
    /// <returns>A nonnull <see cref="AgentError"/> with no retry advice, no known external effect, and this type's stable origin.</returns>
    private static AgentError Error(AgentErrorCode code, string safeMessage, ExtensionData? diagnostics = null) => new(
        code, safeMessage, isRetryable: false, SideEffectCertainty.NotApplicable, _origin,
        externalCode: null, operationId: null, externalRequestId: null, retryAfter: null, diagnostics ?? ExtensionData.Empty);

    /// <summary>Builds a small safe diagnostic bag from string-valued entries.</summary>
    /// <param name="entries">The named string values to retain.</param>
    /// <returns>An <see cref="ExtensionData"/> bag with one canonical-JSON string value per entry.</returns>
    private static ExtensionData Diagnostics(params ReadOnlySpan<(string Key, string Value)> entries)
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty;
        foreach (var (key, value) in entries)
        {
            values = values.Add(key, new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(value)]));
        }

        return new ExtensionData(values);
    }

    /// <summary>Maps a budget dimension to the closest stable error code for a limit that carries no full evidence.</summary>
    private static AgentErrorCode DimensionErrorCode(BudgetDimension dimension) =>
        dimension.Value.Contains("tokens", StringComparison.Ordinal) ? AgentErrorCodes.TokenLimit
        : dimension.Value.Contains("cost", StringComparison.Ordinal) ? AgentErrorCodes.CostLimit
        : dimension.Value.Contains("tools", StringComparison.Ordinal) ? AgentErrorCodes.ToolLimit
        : dimension.Value.Contains("context", StringComparison.Ordinal) ? AgentErrorCodes.ContextLimit
        : dimension.Value.Contains("queue", StringComparison.Ordinal) ? AgentErrorCodes.QueueCapacity
        : AgentErrorCodes.RequestLimit;

    /// <summary>Maps a context-preparation failure category to the closest stable error code.</summary>
    private static AgentErrorCode ContextPreparationErrorCode(ContextPreparationFailureKind kind) => kind switch
    {
        ContextPreparationFailureKind.EmptyHistory => AgentErrorCodes.InvalidInput,
        ContextPreparationFailureKind.BrokenToolCallCausality => AgentErrorCodes.CorruptState,
        ContextPreparationFailureKind.InvalidRolePartCombination => AgentErrorCodes.CorruptState,
        ContextPreparationFailureKind.InvalidInstructionMessage => AgentErrorCodes.InvalidConfiguration,
        ContextPreparationFailureKind.Unknown => AgentErrorCodes.Unknown,
        _ => AgentErrorCodes.Unknown,
    };

    /// <summary>Maps a normalized provider failure category to the closest stable error code.</summary>
    private static AgentErrorCode ProviderFailureErrorCode(ProviderFailureKind kind) => kind switch
    {
        ProviderFailureKind.Authentication => AgentErrorCodes.AuthenticationFailed,
        ProviderFailureKind.Authorization => AgentErrorCodes.AuthorizationDenied,
        ProviderFailureKind.Throttling => AgentErrorCodes.RateLimited,
        ProviderFailureKind.InvalidRequest => AgentErrorCodes.InvalidProviderRequest,
        ProviderFailureKind.Unavailable => AgentErrorCodes.ProviderUnavailable,
        ProviderFailureKind.Timeout => AgentErrorCodes.Timeout,
        ProviderFailureKind.Cancellation => AgentErrorCodes.Cancelled,
        ProviderFailureKind.ProtocolViolation => AgentErrorCodes.ProtocolViolation,
        ProviderFailureKind.Unknown => AgentErrorCodes.Unknown,
        _ => AgentErrorCodes.Unknown,
    };
}
