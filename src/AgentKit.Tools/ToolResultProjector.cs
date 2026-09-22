// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

/// <summary>Projects authoritative terminal records into bounded <see cref="ToolResultPart"/> values.</summary>
public sealed class ToolResultProjector: IToolResultProjector
{
    /// <inheritdoc/>
    public ValueTask<ToolResultPart> ProjectAsync(
        ToolCallResult result,
        ToolResultProjectionPolicySnapshot policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNotEqual(result.ProjectionPolicy, policy.Reference, nameof(policy));

        var resolvedTool = result.ToolId is { } toolId && result.ToolVersion is { } toolVersion
            ? new ToolReference(result.ProviderAlias, toolId, toolVersion)
            : new ToolReference(result.ProviderAlias, null, null);

        var outcome = new ToolCallOutcome(
            result.Status.ToOutcomeKind(),
            result.Status,
            result.SideEffectCertainty,
            result.Retryable,
            result.Error?.SafeMessage,
            ExtensionData.Empty);

        var part = new ToolResultPart(
            result.CallId,
            resolvedTool,
            outcome,
            MapContent(result.Content),
            new ToolResultProjectionInfo(
                result.ProjectionPolicy,
                Enum.IsDefined(result.Status) ? [] : [ToolResultProjectionLoss.StatusCoarsened],
                0,
                0),
            ExtensionData.Empty);

        return ValueTask.FromResult(part);
    }

    private static ImmutableArray<ContentPart> MapContent(ImmutableArray<ToolResultContent> content)
    {
        if (content.IsDefault || content.Length == 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<ContentPart>(content.Length);
        foreach (var item in content)
        {
            if (item is ToolResultTextContent text)
            {
                builder.Add(new TextPart(text.Text, text.Semantics, text.Extensions));
            }
        }

        return builder.ToImmutable();
    }
}
