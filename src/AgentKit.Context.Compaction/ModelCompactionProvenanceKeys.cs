// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// The <see cref="ExtensionData"/> keys under which <see cref="ModelCompactionStrategy"/>
/// records model provenance on the <see cref="CompactionProducer"/> it
/// returns.
/// </summary>
/// <remarks>
/// <para>
/// The reduced <see cref="CompactionProducer"/> shape carries only a
/// strategy key and a determinism flag, so the model, provider, request,
/// response, usage, and truncation evidence a model-backed strategy must
/// record live in its <see cref="CompactionProducer.Extensions"/> under these
/// stable keys. Every value is canonical JSON: strings for identities,
/// integers for counts, and booleans for flags.
/// </para>
/// <para>
/// These values are provenance, not content: no prompt, transcript, or
/// summary text is recorded under any of these keys.
/// </para>
/// </remarks>
public static class ModelCompactionProvenanceKeys
{
    /// <summary>The kind of producer; always the JSON string <c>"model-backed"</c> for this strategy.</summary>
    public const string ProducerKind = "agentkit.compaction.producer.kind";

    /// <summary>The catalog <see cref="ModelAlias"/> the selector chose, as a JSON string.</summary>
    public const string ModelAlias = "agentkit.compaction.model.alias";

    /// <summary>The <see cref="ProviderId"/> that served the request, as a JSON string.</summary>
    public const string ProviderId = "agentkit.compaction.model.provider";

    /// <summary>The <see cref="ModelId"/> the provider reported it resolved, as a JSON string.</summary>
    public const string ModelId = "agentkit.compaction.model.id";

    /// <summary>The <see cref="ModelRequestId"/> of the single summary attempt, as a JSON string.</summary>
    public const string ModelRequestId = "agentkit.compaction.model.request_id";

    /// <summary>The provider's <see cref="ProviderResponseId"/> when it reported one, as a JSON string.</summary>
    public const string ProviderResponseId = "agentkit.compaction.model.response_id";

    /// <summary>The reported input token count when usage was reported, as a JSON integer.</summary>
    public const string InputTokens = "agentkit.compaction.model.usage.input_tokens";

    /// <summary>The reported output token count when usage was reported, as a JSON integer.</summary>
    public const string OutputTokens = "agentkit.compaction.model.usage.output_tokens";

    /// <summary>The number of transcript characters sent after bounding, as a JSON integer.</summary>
    public const string InputCharacters = "agentkit.compaction.summary.input_characters";

    /// <summary>Whether the transcript was truncated to <c>MaximumSummaryInputCharacters</c>, as a JSON boolean.</summary>
    public const string InputTruncated = "agentkit.compaction.summary.input_truncated";

    /// <summary>Whether the summary was truncated to <c>MaximumCheckpointCharacters</c>, as a JSON boolean.</summary>
    public const string OutputTruncated = "agentkit.compaction.summary.output_truncated";
}
