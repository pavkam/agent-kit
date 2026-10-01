// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

/// <summary>Builds the authoritative terminal <see cref="ToolCallResult"/> for each path a call can take through the executor and scheduler.</summary>
/// <remarks>
/// Every result is built from retained evidence: the admission evidence of the request, the accepted record (when the call
/// was accepted), and the captured normalization snapshot. No result is reconstructed from the invoker context.
/// </remarks>
internal static class ToolCallResultComposer
{
    /// <summary>Builds the terminal record for an accepted call whose invoker ran at least once.</summary>
    /// <param name="entry">The prepared and accepted batch entry that was invoked.</param>
    /// <param name="invocation">The raw evidence of the final attempt.</param>
    /// <param name="normalization">The normalized terminal content decision.</param>
    /// <param name="invocationStartedAt">When the final attempt started.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <returns>The authoritative terminal record carrying the accepted call's acceptance evidence.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    internal static ToolCallResult FromAcceptedInvocation(
        ToolBatchEntry entry,
        ToolInvocationResult invocation,
        ToolResultNormalizationResult normalization,
        DateTimeOffset invocationStartedAt,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(normalization);
        var accepted = entry.Accepted;
        var outcome = invocation.Outcome;
        if (normalization is ToolResultNormalizationFailed failed)
        {
            return Accepted(
                accepted,
                failed.Status,
                [],
                new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
                new ToolError(ToolErrorKind.Serialization, failed.SafeReason, null, null, ExtensionData.Empty),
                outcome.SideEffectCertainty,
                outcome.Retryable,
                invocationStartedAt,
                completedAt);
        }

        var normalized = (ToolResultNormalized) normalization;
        var succeeded = outcome.Kind == ToolCallOutcomeKind.Success;
        ToolError? error = null;
        if (!succeeded && outcome.FailureReason is { Length: > 0 } reason)
        {
            error = new ToolError(ToolErrorKind.Tool, reason, null, null, ExtensionData.Empty);
        }

        return Accepted(
            accepted,
            succeeded ? ToolTerminalStatus.Succeeded : outcome.SourceStatus,
            normalized.Content,
            normalized.Info,
            error,
            outcome.SideEffectCertainty,
            outcome.Retryable,
            invocationStartedAt,
            completedAt);
    }

    /// <summary>Builds the terminal record for an accepted call whose invoker never started.</summary>
    /// <param name="entry">The accepted batch entry that was released without invocation.</param>
    /// <param name="status">The rejected or interrupted terminal status.</param>
    /// <param name="safeReason">A bounded safe explanation.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <returns>A terminal record with acceptance evidence, no invocation start, and certainty <see cref="SideEffectCertainty.DefinitelyNotPerformed"/>.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    internal static ToolCallResult AcceptedNotStarted(
        ToolBatchEntry entry,
        ToolTerminalStatus status,
        string safeReason,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        return Accepted(
            entry.Accepted,
            status,
            [],
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            new ToolError(ToolErrorKind.Host, safeReason, null, null, ExtensionData.Empty),
            SideEffectCertainty.DefinitelyNotPerformed,
            retryable: false,
            invocationStartedAt: null,
            completedAt);
    }

    /// <summary>Builds the terminal record for an accepted call that was cancelled while its invoker ran.</summary>
    /// <param name="entry">The accepted batch entry.</param>
    /// <param name="invocationStartedAt">When the interrupted attempt started.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <returns>An interrupted terminal record whose side-effect certainty is <see cref="SideEffectCertainty.Unknown"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    internal static ToolCallResult AcceptedInterrupted(ToolBatchEntry entry, DateTimeOffset invocationStartedAt, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Accepted(
            entry.Accepted,
            ToolTerminalStatus.Interrupted,
            [],
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            new ToolError(ToolErrorKind.Host, "The tool invocation was interrupted.", null, null, ExtensionData.Empty),
            SideEffectCertainty.Unknown,
            retryable: false,
            invocationStartedAt,
            completedAt);
    }

    /// <summary>Builds a pre-invocation rejection that never became an accepted call.</summary>
    /// <param name="request">The admitted request whose identity and raw-argument evidence the result retains.</param>
    /// <param name="status">The rejection status.</param>
    /// <param name="safeReason">A bounded safe explanation.</param>
    /// <param name="toolId">The resolved tool, or null when the alias never resolved.</param>
    /// <param name="toolVersion">The resolved version; present exactly when <paramref name="toolId"/> is.</param>
    /// <param name="effects">The declared effects of the resolved descriptor, or null.</param>
    /// <param name="normalization">The captured normalization rules governing the rejection.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <param name="grantId">A grant that was issued before the rejection, or null when none was.</param>
    /// <returns>A terminal record with <see cref="SideEffectCertainty.DefinitelyNotPerformed"/> and no acceptance evidence.</returns>
    internal static ToolCallResult PreInvocation(
        ToolCallRequest request,
        ToolTerminalStatus status,
        string safeReason,
        ToolId? toolId,
        ToolVersion? toolVersion,
        ToolEffects? effects,
        ToolResultNormalizationSnapshot normalization,
        DateTimeOffset completedAt,
        GrantId? grantId = null) =>
        new(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.TurnId,
            request.OperationId,
            request.CallId,
            request.Authorization,
            grantId,
            acceptance: null,
            request.ProviderAlias,
            toolId,
            toolVersion,
            effects,
            externalIdempotencyKey: null,
            Admission(request),
            status,
            [],
            new ToolError(ToolErrorKind.Tool, safeReason, null, null, ExtensionData.Empty),
            SideEffectCertainty.DefinitelyNotPerformed,
            usage: null,
            retryable: false,
            normalization,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            normalization.ProjectionPolicy,
            request.RequestedAt,
            invocationStartedAt: null,
            completedAt,
            ExtensionData.Empty);

    /// <summary>Builds a rejection for a call whose arguments failed validation.</summary>
    /// <param name="failure">The validation failure carrying the resolved call.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <returns>A resolved, pre-invocation terminal record.</returns>
    internal static ToolCallResult FromValidationFailure(ToolCallValidationFailed failure, DateTimeOffset completedAt) =>
        PreInvocation(
            ToRequest(failure.Call),
            failure.Status,
            failure.SafeReason,
            failure.Call.Tool.Id,
            failure.Call.ToolVersion,
            failure.Call.Tool.Effects,
            ToolRuntimeNormalizationDefaults.ForResolvedTool(failure.Call.ExecutionPolicy),
            completedAt);

    /// <summary>Copies a terminal record with its content removed, as the session terminal entry persists it.</summary>
    /// <param name="result">The authoritative terminal record.</param>
    /// <returns>An equal record whose <see cref="ToolCallResult.Content"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    internal static ToolCallResult WithoutContent(ToolCallResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Content.IsEmpty
            ? result
            : new ToolCallResult(
                result.AgentId,
                result.SessionId,
                result.RunId,
                result.TurnId,
                result.OperationId,
                result.CallId,
                result.Authorization,
                result.GrantId,
                result.Acceptance,
                result.ProviderAlias,
                result.ToolId,
                result.ToolVersion,
                result.Effects,
                result.ExternalIdempotencyKey,
                result.Admission,
                result.Status,
                [],
                result.Error,
                result.SideEffectCertainty,
                result.Usage,
                result.Retryable,
                result.Normalization,
                result.NormalizationInfo,
                result.ProjectionPolicy,
                result.RequestedAt,
                result.InvocationStartedAt,
                result.CompletedAt,
                result.Extensions);
    }

    private static ToolCallResult Accepted(
        AcceptedToolCall accepted,
        ToolTerminalStatus status,
        ImmutableArray<ToolResultContent> content,
        ToolResultNormalizationInfo info,
        ToolError? error,
        SideEffectCertainty certainty,
        bool retryable,
        DateTimeOffset? invocationStartedAt,
        DateTimeOffset completedAt)
    {
        var possiblyStarted = invocationStartedAt.HasValue || certainty is not SideEffectCertainty.DefinitelyNotPerformed;
        var safeToRetry = accepted.Effects.Effect is not ToolEffect.Mutating
            || !possiblyStarted
            || accepted.Effects.Idempotency is IdempotencyClassification.Idempotent
            || (accepted.Effects.Idempotency is IdempotencyClassification.IdempotentWithKey && accepted.ExternalIdempotencyKey.HasValue);
        return new ToolCallResult(
            accepted.AgentId,
            accepted.SessionId,
            accepted.RunId,
            accepted.TurnId,
            accepted.OperationId,
            accepted.CallId,
            accepted.Authorization,
            accepted.Acceptance.InvocationGrantId,
            accepted.Acceptance,
            accepted.ProviderAlias,
            accepted.ToolId,
            accepted.ToolVersion,
            accepted.Effects,
            accepted.ExternalIdempotencyKey,
            accepted.Admission,
            status,
            content,
            status is ToolTerminalStatus.Succeeded ? null : error,
            certainty,
            usage: null,
            retryable && safeToRetry,
            accepted.Normalization,
            info,
            accepted.ProjectionPolicy,
            accepted.RequestedAt,
            invocationStartedAt,
            completedAt,
            ExtensionData.Empty);
    }

    private static ToolCallAdmissionEvidence Admission(ToolCallRequest request) =>
        new(
            request.CatalogVersion,
            request.SourceOrdinal,
            ToolInvocationSecurityBinding.RawAdmissionFingerprint(request.RawArguments));

    private static ToolCallRequest ToRequest(ResolvedToolCall call) =>
        new(
            call.AgentId,
            call.SessionId,
            call.RunId,
            call.TurnId,
            call.OperationId,
            call.CallId,
            call.Authorization,
            call.CatalogVersion,
            call.SourceOrdinal,
            call.ProviderAlias,
            call.RawArguments,
            call.RequestedAt);
}
