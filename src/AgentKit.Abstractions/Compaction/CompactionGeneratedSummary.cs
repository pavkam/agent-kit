// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One generated summary returned by a compaction summary generator.</summary>
public sealed record CompactionGeneratedSummary
{
    /// <summary>Initializes a new instance of the <see cref="CompactionGeneratedSummary"/> record.</summary>
    /// <param name="content">The summary content parts.</param>
    /// <param name="state">Extracted state references carried with the summary.</param>
    /// <param name="providerId">The provider identity when model-backed.</param>
    /// <param name="modelId">The model identity when model-backed.</param>
    /// <param name="modelRevision">The provider model revision when known.</param>
    /// <param name="providerResponseId">The provider response identity when known.</param>
    /// <param name="usage">Reported usage when known.</param>
    /// <param name="extensions">Forward-compatible summary data.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public CompactionGeneratedSummary(
        ImmutableArray<ContentPart> content,
        ImmutableArray<CompactionStateReference> state,
        ProviderId? providerId,
        ModelId? modelId,
        ProviderModelRevision? modelRevision,
        ProviderResponseId? providerResponseId,
        ModelUsage? usage,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(content);
        if (content.IsEmpty)
        {
            throw new ArgumentException("Summary content must contain at least one part.", nameof(content));
        }

        ArgumentException.ThrowIfDefault(state);
        ArgumentNullException.ThrowIfNull(extensions);
        Content = content;
        State = state;
        ProviderId = providerId;
        ModelId = modelId;
        ModelRevision = modelRevision;
        ProviderResponseId = providerResponseId;
        Usage = usage;
        Extensions = extensions;
    }

    /// <summary>Gets the summary content parts.</summary>
    public ImmutableArray<ContentPart> Content { get; }

    /// <summary>Gets extracted state references carried with the summary.</summary>
    public ImmutableArray<CompactionStateReference> State { get; }

    /// <summary>Gets the provider identity when model-backed.</summary>
    public ProviderId? ProviderId { get; }

    /// <summary>Gets the model identity when model-backed.</summary>
    public ModelId? ModelId { get; }

    /// <summary>Gets the provider model revision when known.</summary>
    public ProviderModelRevision? ModelRevision { get; }

    /// <summary>Gets the provider response identity when known.</summary>
    public ProviderResponseId? ProviderResponseId { get; }

    /// <summary>Gets reported usage when known.</summary>
    public ModelUsage? Usage { get; }

    /// <summary>Gets forward-compatible summary data.</summary>
    public ExtensionData Extensions { get; }
}
