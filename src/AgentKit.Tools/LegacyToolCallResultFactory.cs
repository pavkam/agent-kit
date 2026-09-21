// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>
/// Maps legacy <see cref="ResolvedToolInvocation"/> evidence into spec-shaped <see cref="ToolCallResult"/> records for
/// the adapter executor.
/// </summary>
/// <remarks>
/// This factory synthesizes minimal normalization and acceptance evidence that the legacy orchestrator never retained.
/// It is an interim bridge only; <see cref="IToolExecutor"/> replaces it in workstream 4 chunk C5a.
/// </remarks>
internal static class LegacyToolCallResultFactory
{
    private static readonly ToolResultNormalizationSnapshot _normalization = new(
        new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("legacy"), new ToolResultRejectionPolicyVersion(1)),
        ToolResultProjectionPolicyReference.Default,
        executionPolicy: null,
        new ToolResultNormalizationAlgorithmVersion(1),
        new ToolResultBounds(4_194_304, 64),
        ToolResultProjectionTransformations.None,
        ExtensionData.Empty);

    /// <summary>Builds one terminal <see cref="ToolCallResult"/> from legacy orchestrator output.</summary>
    /// <param name="request">The spec-shaped request that initiated the legacy call.</param>
    /// <param name="resolved">The legacy orchestrator outcome.</param>
    /// <param name="completedAt">The terminal timestamp.</param>
    /// <returns>A structurally valid terminal record for loop projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="resolved"/> is null.</exception>
    internal static ToolCallResult Create(ToolCallRequest request, ResolvedToolInvocation resolved, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolved);

        var invocation = resolved.Invocation;
        var outcome = invocation.Outcome;
        var resolvedTool = resolved.Tool;
        var toolId = resolvedTool.Id;
        var toolVersion = resolvedTool.Version;
        var hasResolvedTool = toolId.HasValue && toolVersion.HasValue;
        var succeeded = outcome.Kind == ToolCallOutcomeKind.Success;
        var status = succeeded ? ToolTerminalStatus.Succeeded : outcome.SourceStatus;

        ToolCallAcceptanceEvidence? acceptance = null;
        GrantId? grantId = null;
        DateTimeOffset? startedAt = null;
        ToolEffects? effects = null;
        if (succeeded && hasResolvedTool)
        {
            grantId = new GrantId(request.CallId.Value);
            acceptance = new ToolCallAcceptanceEvidence(
                grantId.Value,
                new InputFingerprint($"legacy:{request.CallId}"),
                request.RequestedAt);
            startedAt = request.RequestedAt;
            effects = new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, []);
        }

        var admission = new ToolCallAdmissionEvidence(
            request.CatalogVersion,
            request.SourceOrdinal,
            new InputFingerprint($"legacy-raw:{request.CallId}"));

        var content = MapContent(invocation.Content);
        var error = outcome.Kind is ToolCallOutcomeKind.Rejected or ToolCallOutcomeKind.Failed
            ? new ToolError(ToolErrorKind.Tool, outcome.FailureReason ?? "The tool call did not succeed.", null, null, ExtensionData.Empty)
            : null;

        if (succeeded)
        {
            error = null;
        }

        var sideEffectCertainty = outcome.SideEffectCertainty;
        var retryable = outcome.Retryable;

        return new ToolCallResult(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.TurnId,
            request.OperationId,
            request.CallId,
            request.Authorization,
            grantId,
            acceptance,
            request.ProviderAlias,
            hasResolvedTool ? toolId : null,
            hasResolvedTool ? toolVersion : null,
            effects,
            externalIdempotencyKey: null,
            admission,
            status,
            content,
            error,
            sideEffectCertainty,
            usage: null,
            retryable,
            _normalization,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
            resolved.ProjectionPolicy,
            request.RequestedAt,
            startedAt,
            completedAt,
            ExtensionData.Empty);
    }

    private static ImmutableArray<ToolResultContent> MapContent(ImmutableArray<ContentPart> parts)
    {
        if (parts.IsDefault || parts.Length == 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<ToolResultContent>(parts.Length);
        foreach (var part in parts)
        {
            if (part is TextPart text)
            {
                builder.Add(new ToolResultTextContent(text.Text, text.Semantics, text.Extensions));
            }
            else if (part is ToolResultPart)
            {
                continue;
            }
            else
            {
                builder.Add(new ToolResultOpaqueContent(
                    "legacy.content-part",
                    new ExtensionValue([(byte) '{', (byte) '}']),
                    ExtensionData.Empty));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Parses bounded raw argument bytes into a <see cref="JsonElement"/> for the legacy orchestrator.</summary>
    /// <param name="rawArguments">The initialized raw bytes from a <see cref="ToolCallRequest"/>.</param>
    /// <returns>The parsed JSON value, or an empty object when the bytes are empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="rawArguments"/> is uninitialized.</exception>
    internal static JsonElement ParseRawArguments(ImmutableArray<byte> rawArguments)
    {
        ArgumentException.ThrowIfDefault(rawArguments);
        if (rawArguments.Length == 0)
        {
            return JsonDocument.Parse("{}").RootElement;
        }

        using var document = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(rawArguments.AsSpan()));
        return document.RootElement.Clone();
    }
}
