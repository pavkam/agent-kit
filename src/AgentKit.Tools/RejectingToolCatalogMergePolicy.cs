// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Selects collision-free captured catalogs and rejects every unconfigured ambiguity or missing target.</summary>
/// <remarks>This stateless, concurrently callable default never infers precedence or aliases. Hosts replace the policy to select explicitly configured existing contributions. It performs no provider I/O, invoker acquisition, or authorization.</remarks>
public sealed class RejectingToolCatalogMergePolicy: IToolCatalogMergePolicy
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RejectingToolCatalogMergePolicy> _logger;

    /// <summary>Captures isolated diagnostic dependencies without performing observation at construction.</summary>
    /// <param name="timeProvider">The nonnull diagnostic clock.</param>
    /// <param name="logger">The nonnull type-specific safe logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public RejectingToolCatalogMergePolicy(TimeProvider timeProvider, ILogger<RejectingToolCatalogMergePolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>Completes synchronously. An empty collision-free graph is a valid empty selection. The caller still validates the returned decision against the original source graph.</remarks>
    public ValueTask<ToolCatalogMergeDecision> ResolveAsync(ToolCatalogMergeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var observation = new ToolCatalogMergeObservation(true, context.Request, _timeProvider, _logger);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.Collisions.IsEmpty)
            {
                observation.Complete("rejected");
                return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection());
            }
            var aliases = ImmutableDictionary.CreateBuilder<ToolAlias, ToolCatalogCandidate>();
            foreach (var candidate in context.Candidates)
            {
                foreach (var assignment in candidate.Toolset.Aliases.Where(assignment => assignment.Tool == candidate.Identity))
                {
                    if (!aliases.TryAdd(assignment.Alias, candidate))
                    {
                        observation.Complete("rejected");
                        return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection());
                    }
                }
            }
            if (context.Candidates.Select(static candidate => candidate.Identity).Distinct().Count() != context.Candidates.Length)
            {
                observation.Complete("rejected");
                return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection());
            }
            var selection = new ToolCatalogSelection(context.Candidates, aliases.ToImmutable());
            cancellationToken.ThrowIfCancellationRequested();
            observation.Complete("selected");
            return ValueTask.FromResult<ToolCatalogMergeDecision>(selection);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled");
            throw;
        }
        catch
        {
            observation.Complete("failed");
            throw;
        }
    }
}
