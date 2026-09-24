// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Collections.Immutable;

/// <summary>Builds minimal <see cref="ToolCallResult"/> values for loop tests.</summary>
internal static class TestToolCallResults
{
    private static readonly ToolExecutionPolicyReference ExecutionPolicy = new(
        new ToolExecutionPolicyKey("test"),
        new ToolExecutionPolicyVersion(1));

    private static ToolResultNormalizationSnapshot NormalizationFor(bool resolvedTool) => new(
        new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("test"), new ToolResultRejectionPolicyVersion(1)),
        ToolResultProjectionPolicyReference.Default,
        executionPolicy: resolvedTool ? ExecutionPolicy : null,
        new ToolResultNormalizationAlgorithmVersion(1),
        new ToolResultBounds(1024, 4),
        ToolResultProjectionTransformations.None,
        ExtensionData.Empty);

    internal static ToolCallResult FromInvocation(ToolCallRequest call, ToolInvocationResult invocation)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(invocation);
        var outcome = invocation.Outcome;
        var succeeded = outcome.Kind == ToolCallOutcomeKind.Success;
        var hasTool = outcome.SourceStatus != ToolTerminalStatus.UnknownTool;
        ToolCallAcceptanceEvidence? acceptance = null;
        GrantId? grantId = null;
        DateTimeOffset? startedAt = null;
        var effects = hasTool
            ? new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, [])
            : null;
        if (succeeded)
        {
            grantId = new GrantId(call.CallId.Value);
            acceptance = new ToolCallAcceptanceEvidence(grantId.Value, new InputFingerprint("test"), call.RequestedAt);
            startedAt = call.RequestedAt;
        }

        var content = ImmutableArray<ToolResultContent>.Empty;
        foreach (var part in invocation.Content)
        {
            if (part is TextPart text)
            {
                content = content.Add(new ToolResultTextContent(text.Text, text.Semantics, text.Extensions));
            }
        }

        ToolError? error = null;
        if (!succeeded && outcome.FailureReason is { Length: > 0 } failureReason)
        {
            error = new ToolError(ToolErrorKind.Tool, failureReason, null, null, ExtensionData.Empty);
        }

        var normalization = NormalizationFor(hasTool);
        ToolId? toolId = hasTool ? new ToolId(call.ProviderAlias.Value) : null;
        ToolVersion? toolVersion = hasTool ? new ToolVersion("1") : null;

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
            toolId,
            toolVersion,
            effects,
            null,
            new ToolCallAdmissionEvidence(call.CatalogVersion, call.SourceOrdinal, new InputFingerprint("test")),
            succeeded ? ToolTerminalStatus.Succeeded : outcome.SourceStatus,
            content,
            error,
            outcome.SideEffectCertainty,
            null,
            outcome.Retryable,
            normalization,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            ToolResultProjectionPolicyReference.Default,
            call.RequestedAt,
            startedAt,
            call.RequestedAt,
            ExtensionData.Empty);
    }

    internal static ToolCallResult FromPreInvocation(
        ToolCallRequest call,
        ToolTerminalStatus status,
        string safeReason)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        return new ToolCallResult(
            call.AgentId,
            call.SessionId,
            call.RunId,
            call.TurnId,
            call.OperationId,
            call.CallId,
            call.Authorization,
            grantId: null,
            acceptance: null,
            call.ProviderAlias,
            toolId: null,
            toolVersion: null,
            effects: null,
            externalIdempotencyKey: null,
            admission: new ToolCallAdmissionEvidence(call.CatalogVersion, call.SourceOrdinal, new InputFingerprint("test")),
            status,
            content: [],
            error: new ToolError(ToolErrorKind.Tool, safeReason, null, null, ExtensionData.Empty),
            SideEffectCertainty.DefinitelyNotPerformed,
            usage: null,
            retryable: false,
            NormalizationFor(resolvedTool: false),
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            ToolResultProjectionPolicyReference.Default,
            call.RequestedAt,
            invocationStartedAt: null,
            call.RequestedAt,
            ExtensionData.Empty);
    }
}
