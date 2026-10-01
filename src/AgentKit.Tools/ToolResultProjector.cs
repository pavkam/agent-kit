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

        var externalized = !result.Content.IsDefaultOrEmpty && result.Content.Any(static item => item is ToolResultArtifactContent);
        var part = new ToolResultPart(
            result.CallId,
            resolvedTool,
            outcome,
            MapContent(result.Content),
            new ToolResultProjectionInfo(
                result.ProjectionPolicy,
                ProjectionLosses(result.Status, externalized),
                0,
                0),
            ExtensionData.Empty);

        return ValueTask.FromResult(part);
    }

    private static ImmutableArray<ToolResultProjectionLoss> ProjectionLosses(ToolTerminalStatus status, bool externalized)
    {
        var losses = ImmutableArray.CreateBuilder<ToolResultProjectionLoss>(2);
        if (externalized)
        {
            losses.Add(ToolResultProjectionLoss.Externalized);
        }

        if (!Enum.IsDefined(status))
        {
            losses.Add(ToolResultProjectionLoss.StatusCoarsened);
        }

        return losses.ToImmutable();
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
            else if (item is ToolResultArtifactContent artifact)
            {
                var reference = artifact.Reference;
                builder.Add(new TextPart(
                    $"The complete tool output was stored as artifact {reference.Id} version {reference.Version} ({reference.Length} bytes, {reference.MediaType}, {reference.Integrity.ContentHash}).",
                    TextSemantics.Plain,
                    ExtensionData.Empty));
            }
        }

        return builder.ToImmutable();
    }
}
