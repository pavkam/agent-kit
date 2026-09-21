// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Collections.Immutable;

/// <summary>Builds minimal <see cref="ToolCallResult"/> values for loop tests.</summary>
internal static class TestToolCallResults
{
    private static readonly ToolResultNormalizationSnapshot Normalization = new(
        new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("test"), new ToolResultRejectionPolicyVersion(1)),
        ToolResultProjectionPolicyReference.Default,
        executionPolicy: null,
        new ToolResultNormalizationAlgorithmVersion(1),
        new ToolResultBounds(1024, 4),
        ToolResultProjectionTransformations.None,
        ExtensionData.Empty);

    internal static ToolCallResult FromResolved(ToolCallRequest call, ResolvedToolInvocation resolved)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(resolved);
        var invocation = resolved.Invocation;
        var outcome = invocation.Outcome;
        var succeeded = outcome.Kind == ToolCallOutcomeKind.Success;
        var toolId = resolved.Tool.Id;
        var toolVersion = resolved.Tool.Version;
        var hasTool = toolId.HasValue && toolVersion.HasValue;
        ToolCallAcceptanceEvidence? acceptance = null;
        GrantId? grantId = null;
        DateTimeOffset? startedAt = null;
        ToolEffects? effects = null;
        if (succeeded && hasTool)
        {
            grantId = new GrantId(call.CallId.Value);
            acceptance = new ToolCallAcceptanceEvidence(grantId.Value, new InputFingerprint("test"), call.RequestedAt);
            startedAt = call.RequestedAt;
            effects = new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, []);
        }

        var content = ImmutableArray<ToolResultContent>.Empty;
        foreach (var part in invocation.Content)
        {
            if (part is TextPart text)
            {
                content = content.Add(new ToolResultTextContent(text.Text, text.Semantics, text.Extensions));
            }
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
            hasTool ? toolId : null,
            hasTool ? toolVersion : null,
            effects,
            null,
            new ToolCallAdmissionEvidence(call.CatalogVersion, call.SourceOrdinal, new InputFingerprint("test")),
            succeeded ? ToolTerminalStatus.Succeeded : outcome.SourceStatus,
            content,
            succeeded ? null : new ToolError(ToolErrorKind.Tool, outcome.FailureReason ?? "failed", null, null, ExtensionData.Empty),
            outcome.SideEffectCertainty,
            null,
            outcome.Retryable,
            Normalization,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            resolved.ProjectionPolicy,
            call.RequestedAt,
            startedAt,
            call.RequestedAt,
            ExtensionData.Empty);
    }
}
