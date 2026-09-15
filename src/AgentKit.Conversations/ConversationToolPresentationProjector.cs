// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Projects durable or live tool parts through the same exact captured presenter binding.</summary>
internal static class ConversationToolPresentationProjector
{
    /// <summary>Presents one tool call or result without allowing observational failure to affect conversation truth.</summary>
    /// <param name="part">The immutable tool call or result projection.</param>
    /// <param name="toolPresenter">The optional bounded presenter.</param>
    /// <param name="bindings">Exact descriptor bindings captured with advertised tools.</param>
    /// <param name="cancellationToken">Cancels presentation.</param>
    /// <returns>The bounded presentation, or null when presentation is unavailable or fails.</returns>
    internal static async ValueTask<ToolPresentation?> PresentAsync(
        ContentPart part,
        IToolPresenter? toolPresenter,
        ImmutableDictionary<ToolId, ConversationToolPresentationBinding> bindings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentException.ThrowIfNotEqual(part is ToolCallPart or ToolResultPart, true, nameof(part));
        cancellationToken.ThrowIfCancellationRequested();
        if (toolPresenter is null)
        {
            return null;
        }

        var tool = part is ToolCallPart call ? call.Tool : ((ToolResultPart) part).Tool;
        var descriptor = bindings.TryGetValue(tool.Id, out var binding)
            && (tool.Version is null || binding.Descriptor.Version == tool.Version)
            && string.Equals(binding.AdvertisedTool.Name, tool.Name, StringComparison.Ordinal)
                ? binding.Descriptor
                : null;
        ToolPresentationSource source = part is ToolCallPart toolCall
            ? new ToolCallPresentationSource(toolCall)
            : new ToolResultPresentationSource((ToolResultPart) part);
        try
        {
            return await toolPresenter.PresentAsync(
                new ToolPresentationRequest(descriptor, source, new ToolPresentationBounds()),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
