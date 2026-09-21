// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using System.Collections.Immutable;

/// <summary>Projects authoritative <see cref="ToolCallResult"/> records into model-facing <see cref="ToolResultPart"/> values.</summary>
internal static class ToolCallResultProjection
{
    /// <summary>Creates one terminal tool result part from an authoritative record.</summary>
    /// <param name="result">The terminal tool-call record.</param>
    /// <param name="requestedTool">The tool reference from the originating <see cref="ToolCallPart"/>.</param>
    /// <returns>A bounded projection suitable for session commit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    internal static ToolResultPart ToToolResultPart(ToolCallResult result, ToolReference requestedTool)
    {
        ArgumentNullException.ThrowIfNull(result);

        var resolvedTool = result.ToolId is { } toolId && result.ToolVersion is { } toolVersion
            ? new ToolReference(result.ProviderAlias, toolId, toolVersion)
            : requestedTool;

        var outcome = new ToolCallOutcome(
            result.Status.ToOutcomeKind(),
            result.Status,
            result.SideEffectCertainty,
            result.Retryable,
            result.Error?.SafeMessage,
            ExtensionData.Empty);

        return new ToolResultPart(
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
            switch (item)
            {
                case ToolResultTextContent text:
                    builder.Add(new TextPart(text.Text, text.Semantics, text.Extensions));
                    break;
                default:
                    break;
            }
        }

        return builder.ToImmutable();
    }
}
