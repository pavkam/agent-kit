// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Runs the authorize, rewrite, select, search, stale-filter, classify, deduplicate, rerank, expose-authorize, and budget stages of one retrieval.</summary>
/// <remarks>
/// <para>
/// Each call asks the profile runtime selector for the exact profile key and version of the query, holds the lease for the whole
/// retrieval, and disposes it, so no collaborator of another agent or profile version is mixed in. The selected security authority
/// issues separate bounded grants for the query, the embedding egress, each source read, each store read, and each candidate's
/// exposure; no grant is reused for a different concrete effect. A denied or unavailable source is skipped and counted, while an
/// unavailable exposure check or required observation fails closed without exposing any candidate.
/// </para>
/// <para>
/// The pipeline proposes authorized candidates and never assembles context. Candidate text is untrusted data and its provenance,
/// identities, and source version survive rewriting, reranking, deduplication, and trimming. Every stale hit is filtered against the
/// authoritative active state before reranking or exposure.
/// </para>
/// </remarks>
internal sealed partial class RetrievalPipeline: IRetrievalPipeline
{
    private static readonly ComponentId _audience = new("agentkit.memory.retrieval");

    private readonly IMemoryProfileRuntimeSelector _runtimes;
    private readonly MemoryGrantIssuer _grants;
    private readonly TimeProvider _time;
    private readonly ILogger<RetrievalPipeline> _logger;

    /// <summary>Initializes the pipeline.</summary>
    /// <param name="runtimes">The selector that activates the exact profile runtime for each retrieval.</param>
    /// <param name="grants">The issuer of single-use grants from the captured authority.</param>
    /// <param name="time">The injected clock for observation and expiry checks.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public RetrievalPipeline(IMemoryProfileRuntimeSelector runtimes, MemoryGrantIssuer grants, TimeProvider time, ILogger<RetrievalPipeline>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(time);
        _runtimes = runtimes;
        _grants = grants;
        _time = time;
        _logger = logger ?? NullLogger<RetrievalPipeline>.Instance;
    }

    /// <inheritdoc/>
    public async Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var started = MemoryObservation.TryTimestamp(_time);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.RetrievalRetrieve,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.RetrievalRequestId, query.Id.ToString()),
                new(AgentKitTagNames.AgentId, query.Context.AgentId.ToString()),
                new(AgentKitTagNames.TenantId, query.Context.Identity.TenantId.Value),
                new(AgentKitTagNames.MemoryProfileKey, query.Context.ProfileKey.Value),
            ]);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = await _runtimes.SelectAsync(query.Context, cancellationToken).ConfigureAwait(false);
            if (selection is not MemoryProfileRuntimeSelected selected)
            {
                return Finish(scope, query, started, RetrievalResult.Failed(query.Id, new RetrievalFailure(
                    RetrievalFailureKind.ProfileUnavailable, ((MemoryProfileRuntimeUnavailable) selection).Failure.SafeMessage)));
            }

            await using var lease = selected.Runtime;
            var result = await RunAsync(query, lease, cancellationToken).ConfigureAwait(false);
            return Finish(scope, query, started, result);
        }
        catch (OperationCanceledException)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            MemoryObservation.Safe(() => MemoryLog.RetrievalCancelled(_logger, query.Id));
            MemoryObservation.Safe(() => MemoryObservation.RecordRetrieval("cancelled", MemoryObservation.TryElapsed(_time, started), null, null));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            MemoryObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            MemoryObservation.Safe(() => MemoryLog.RetrievalFaulted(_logger, query.Id, errorType));
            MemoryObservation.Safe(() => MemoryObservation.RecordRetrieval("faulted", MemoryObservation.TryElapsed(_time, started), null, null));
            throw;
        }
    }

    private RetrievalResult Finish(AgentKitActivityScope scope, RetrievalQuery query, long? started, RetrievalResult result)
    {
        var elapsed = MemoryObservation.TryElapsed(_time, started);
        if (result.IsCompleted)
        {
            var summary = result.Summary;
            MemoryObservation.Safe(() =>
            {
                _ = scope.Activity?.SetTag(AgentKitTagNames.RetrievalCandidateCount, result.Candidates.Length);
                scope.Activity.SetSuccessful("completed");
            });
            MemoryObservation.Safe(() => MemoryLog.RetrievalCompleted(_logger, query.Id, result.Candidates.Length, summary.Searched));
            MemoryObservation.Safe(() => MemoryObservation.RecordRetrieval("completed", elapsed, result.Candidates.Length, summary));
        }
        else
        {
            var kind = KindName(result.Failure.Kind);
            MemoryObservation.Safe(() => scope.Activity.SetFailed(kind, kind));
            MemoryObservation.Safe(() => MemoryLog.RetrievalRejected(_logger, query.Id, kind));
            MemoryObservation.Safe(() => MemoryObservation.RecordRetrieval(kind, elapsed, null, null));
        }

        return result;
    }

    private static string KindName(RetrievalFailureKind kind) => kind switch
    {
        RetrievalFailureKind.Denied => "denied",
        RetrievalFailureKind.ProfileUnavailable => "profile_unavailable",
        RetrievalFailureKind.RetrievalDisabled => "retrieval_disabled",
        RetrievalFailureKind.ClassificationExceeded => "classification_exceeded",
        RetrievalFailureKind.RewriteFailed => "rewrite_failed",
        RetrievalFailureKind.SourcesUnavailable => "sources_unavailable",
        RetrievalFailureKind.EmbeddingUnavailable => "embedding_unavailable",
        RetrievalFailureKind.ExposureUnavailable => "exposure_unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The retrieval failure class is undefined."),
    };
}
