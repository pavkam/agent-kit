// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

/// <summary>Constructs authoritative <see cref="ToolCallResult"/> records for the spec-shaped executor pipeline.</summary>
internal static class ToolCallResultComposer
{
    internal static ToolCallResult PreInvocation(
        ToolCallRequest request,
        ToolTerminalStatus status,
        string safeReason,
        ToolId? toolId,
        ToolVersion? toolVersion,
        ToolEffects? effects,
        ToolResultNormalizationSnapshot normalization,
        DateTimeOffset completedAt) =>
        new(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.TurnId,
            request.OperationId,
            request.CallId,
            request.Authorization,
            grantId: null,
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

    internal static ToolCallResult FromValidationFailure(
        ToolCallValidationFailed failure,
        DateTimeOffset completedAt) =>
        PreInvocation(
            ToRequest(failure.Call),
            failure.Status,
            failure.SafeReason,
            failure.Call.Tool.Id,
            failure.Call.ToolVersion,
            failure.Call.Tool.Effects,
            ToolRuntimeNormalizationDefaults.ForResolvedTool(failure.Call.ExecutionPolicy),
            completedAt);

    internal static ToolCallResult FromInvocation(
        ToolCallRequest request,
        ValidatedToolCall call,
        ToolInvocationResult invocation,
        ToolResultNormalizationResult normalization,
        SecurityGrant? invocationGrant,
        DateTimeOffset invocationStartedAt,
        DateTimeOffset completedAt)
    {
        var snapshot = ToolRuntimeNormalizationDefaults.ForResolvedTool(call.ExecutionPolicy);
        if (normalization is ToolResultNormalizationFailed failed)
        {
            return Terminal(
                request,
                call,
                failed.Status,
                failed.SafeReason,
                invocationGrant?.Id,
                null,
                invocationStartedAt,
                completedAt,
                snapshot,
                [],
                new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
                invocation.Outcome.Retryable,
                invocation.Outcome.SideEffectCertainty);
        }

        var normalized = (ToolResultNormalized) normalization;
        var succeeded = invocation.Outcome.Kind == ToolCallOutcomeKind.Success;
        var status = succeeded ? ToolTerminalStatus.Succeeded : invocation.Outcome.SourceStatus;
        ToolCallAcceptanceEvidence? acceptance = null;
        var grantId = invocationGrant?.Id;
        if (succeeded && invocationGrant is not null)
        {
            acceptance = new ToolCallAcceptanceEvidence(
                invocationGrant.Id,
                call.InputFingerprint,
                invocationStartedAt);
        }

        ToolError? error = null;
        if (!succeeded && invocation.Outcome.FailureReason is { Length: > 0 } failureReason)
        {
            error = new ToolError(ToolErrorKind.Tool, failureReason, null, null, ExtensionData.Empty);
        }

        return Terminal(
            request,
            call,
            status,
            null,
            grantId,
            acceptance,
            invocationStartedAt,
            completedAt,
            snapshot,
            normalized.Content,
            normalized.Info,
            invocation.Outcome.Retryable,
            invocation.Outcome.SideEffectCertainty,
            error);
    }

    private static ToolCallResult Terminal(
        ToolCallRequest request,
        ValidatedToolCall call,
        ToolTerminalStatus status,
        string? safeReason,
        GrantId? grantId,
        ToolCallAcceptanceEvidence? acceptance,
        DateTimeOffset invocationStartedAt,
        DateTimeOffset completedAt,
        ToolResultNormalizationSnapshot snapshot,
        ImmutableArray<ToolResultContent> content,
        ToolResultNormalizationInfo normalizationInfo,
        bool retryable,
        SideEffectCertainty sideEffectCertainty,
        ToolError? error = null)
    {
        if (error is null && safeReason is { Length: > 0 })
        {
            error = new ToolError(ToolErrorKind.Tool, safeReason, null, null, ExtensionData.Empty);
        }

        return new ToolCallResult(
            call.AgentId,
            call.SessionId,
            call.RunId,
            call.TurnId,
            call.OperationId,
            call.CallId,
            call.Authorization,
            grantId,
            acceptance,
            call.ProviderAlias,
            call.Tool.Id,
            call.ToolVersion,
            call.Tool.Effects,
            externalIdempotencyKey: null,
            Admission(request),
            status,
            content,
            error,
            sideEffectCertainty,
            usage: null,
            retryable,
            snapshot,
            normalizationInfo,
            snapshot.ProjectionPolicy,
            call.RequestedAt,
            acceptance is null ? null : invocationStartedAt,
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
