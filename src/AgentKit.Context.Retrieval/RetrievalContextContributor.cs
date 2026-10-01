// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval;

using System.Text.Json;

/// <summary>Contributes authorized retrieval results as untrusted reference data for one model request.</summary>
/// <remarks>
/// <para>
/// The contributor reads the agent definition's selected memory profile, builds one <see cref="RetrievalQuery"/> from the latest
/// user message under the request's captured authorization, and runs it through the engine's <see cref="IRetrievalPipeline"/>.
/// Every surviving candidate becomes one <see cref="ContextCandidate"/> of kind <see cref="ContextCandidateKind.ReferenceData"/>
/// with <see cref="ContextTrust.RetrievedData"/> trust: retrieved content is data, never instruction, and its trust never rises
/// above that level regardless of the source's own classification. Candidate source references preserve the memory, document, or
/// chunk identity, and the provenance and sensitivity are kept as candidate extension data.
/// </para>
/// <para>
/// An agent without a memory profile, a request without user text, or a retrieval that is refused, unavailable, or empty
/// contributes no candidates. A refusal adds one content-free warning diagnostic so operators can see it without any query text or
/// retrieved content leaving the retrieval boundary. Cancellation propagates. The contributor holds no mutable state and is safe for
/// concurrent use.
/// </para>
/// </remarks>
public sealed class RetrievalContextContributor: IContextContributor
{
    private static readonly ContextSourceNamespace _namespace = new("agentkit.context.retrieval");

    private readonly IRetrievalPipeline _pipeline;
    private readonly IMemoryProfileCatalog _profiles;
    private readonly IIdentifierGenerator<RetrievalRequestId> _requestIds;
    private readonly RetrievalContextOptions _options;
    private readonly ILogger<RetrievalContextContributor> _logger;

    /// <summary>Initializes the contributor.</summary>
    /// <param name="pipeline">The engine's retrieval pipeline.</param>
    /// <param name="profiles">The compiled memory profile catalog.</param>
    /// <param name="requestIds">The generator of retrieval request identities.</param>
    /// <param name="options">The validated contributor options.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option ceiling is not positive or the classification is undefined.</exception>
    public RetrievalContextContributor(
        IRetrievalPipeline pipeline,
        IMemoryProfileCatalog profiles,
        IIdentifierGenerator<RetrievalRequestId> requestIds,
        IOptions<RetrievalContextOptions> options,
        ILogger<RetrievalContextContributor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfUndefined(options.Value.MaximumClassification);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumItems, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumTokens, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumQueryCharacters, nameof(options));
        _pipeline = pipeline;
        _profiles = profiles;
        _requestIds = requestIds;
        _options = options.Value;
        _logger = logger ?? NullLogger<RetrievalContextContributor>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<ContextContribution> ContributeAsync(ContextContributionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Agent.OptionalCapabilities.MemoryProfile is not { } profileKey
            || !_profiles.TryGet(profileKey, out var profile)
            || !profile.RetrievalEnabled
            || QueryText(request) is not { } text)
        {
            return new ContextContribution([], []);
        }

        var context = new MemoryOperationContext(
            request.Agent.Id, request.SessionId, request.Identity, request.Authorization.Scope.Correlation, request.Authorization, profile.Key, profile.Version);
        var query = new RetrievalQuery(
            _requestIds.Create(),
            context,
            new RetrievalQueryContent(text),
            RetrievalScope.Unrestricted,
            new RetrievalBudget(_options.MaximumItems, _options.MaximumBytes, _options.MaximumTokens),
            _options.MaximumClassification,
            new ModelDestination(request.Model.Alias));
        var result = await _pipeline.RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
        if (!result.IsCompleted)
        {
            var kind = result.Failure.Kind.ToString();
            RetrievalContextLog.RetrievalRefused(_logger, query.Id, kind);
            return new ContextContribution(
                [],
                [new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "retrieval-unavailable", $"Retrieval contributed no context ({kind}).")]);
        }

        var candidates = ImmutableArray.CreateBuilder<ContextCandidate>(result.Candidates.Length);
        for (var rank = 0; rank < result.Candidates.Length; rank++)
        {
            candidates.Add(ToContextCandidate(result.Candidates[rank], _options.Priority - rank));
        }

        RetrievalContextLog.RetrievalContributed(_logger, query.Id, candidates.Count);
        return new ContextContribution(candidates.ToImmutable(), []);
    }

    private string? QueryText(ContextContributionRequest request)
    {
        var message = request.History.Messages.OfType<UserMessage>().LastOrDefault();
        if (message is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in message.Parts.OfType<TextPart>())
        {
            if (builder.Length > 0)
            {
                _ = builder.Append('\n');
            }

            _ = builder.Append(part.Text);
        }

        var text = builder.ToString().Trim();
        return text.Length == 0 ? null : text.Length > _options.MaximumQueryCharacters ? text[.._options.MaximumQueryCharacters] : text;
    }

    private static ContextCandidate ToContextCandidate(RetrievalCandidate candidate, int priority)
    {
        var identity = candidate.ChunkId is { } chunk
            ? $"document:{candidate.DocumentId}:chunk:{chunk}"
            : candidate.MemoryId is { } memory ? $"memory:{memory}" : $"document:{candidate.DocumentId}";
        var bytes = candidate.Content.Utf8Bytes;
        return new ContextCandidate(
            new ContextSourceReference(_namespace, new ContextSourceKey(identity), new ContextSourceVersion(candidate.Source.Version)),
            ContextCandidateKind.ReferenceData,
            ContextTrust.RetrievedData,
            priority,
            ContextScope.ModelRequest,
            new ContextCostEstimate(bytes, Math.Max(1, bytes / 4)),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerModelRequest,
            mandatory: false,
            [new TextPart(candidate.Content.Text, TextSemantics.Plain, ExtensionData.Empty)],
            Provenance(candidate));
    }

    private static ExtensionData Provenance(RetrievalCandidate candidate)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (candidate.ChunkId is { } chunk)
            {
                writer.WriteString("chunkId", chunk.ToString());
            }

            writer.WriteString("classification", candidate.Classification.ToString());
            if (candidate.DocumentId is { } document)
            {
                writer.WriteString("documentId", document.ToString());
            }

            if (candidate.MemoryId is { } memory)
            {
                writer.WriteString("memoryId", memory.ToString());
            }

            writer.WriteString("source", candidate.Source.Key.Value);
            writer.WriteString("sourceKind", candidate.Provenance.SourceKind);
            writer.WriteString("sourceVersion", candidate.Source.Version);
            writer.WriteEndObject();
        }

        return new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("agentkit.retrieval.provenance", new ExtensionValue([.. stream.ToArray()])));
    }
}
