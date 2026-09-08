// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reads the immutable exact run-profile publications captured by one built composition.</summary>
internal sealed class DefaultAgentRunProfilePublicationReader: IAgentRunProfilePublicationReader
{
    private readonly Dictionary<(AgentId AgentId, AgentDefinitionRevision Revision), AgentRunProfilePublication> _byDefinition;
    private readonly ILogger<DefaultAgentRunProfilePublicationReader> _logger;

    /// <summary>Initializes and freezes additive host publications, rejecting ambiguous definition coordinates.</summary>
    /// <param name="publications">The additive non-null host publications.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publications"/> is null.</exception>
    /// <param name="logger">The optional content-free diagnostics logger.</param>
    /// <exception cref="ArgumentException">An item is null or two publications use the same agent and definition revision.</exception>
    public DefaultAgentRunProfilePublicationReader(
        IEnumerable<AgentRunProfilePublication> publications,
        ILogger<DefaultAgentRunProfilePublicationReader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(publications);
        var frozen = publications.ToImmutableArray();
        ArgumentException.ThrowIfDuplicateAgentRunProfileCoordinates(frozen, nameof(publications));
        _byDefinition = [];
        foreach (var publication in frozen)
        {
            var key = (publication.SecurityProfile.AgentId, publication.SecurityProfile.AgentDefinitionRevision);
            _byDefinition.Add(key, publication);
        }

        CurrentSnapshot = new AgentRunProfilePublicationSnapshot(frozen);
        _logger = logger ?? NullLogger<DefaultAgentRunProfilePublicationReader>.Instance;
    }

    /// <inheritdoc/>
    public AgentRunProfilePublicationSnapshot CurrentSnapshot { get; }

    /// <inheritdoc/>
    public ValueTask<AgentRunProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfNegative(
            agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        if (cancellationToken.IsCancellationRequested)
        {
            AgentRunProfilePublicationObservability.Record(_logger, agentId, "cancelled");
            cancellationToken.ThrowIfCancellationRequested();
        }

        AgentRunProfilePublicationResult result =
            _byDefinition.TryGetValue((agentId, agentDefinitionRevision), out var publication)
                ? new AgentRunProfilePublicationFound(publication)
                : new AgentRunProfilePublicationUnavailable(
                    "No run-profile publication matches the exact agent definition.");
        AgentRunProfilePublicationObservability.Record(
            _logger, agentId, result is AgentRunProfilePublicationFound ? "found" : "unavailable");
        return ValueTask.FromResult(result);
    }
}
