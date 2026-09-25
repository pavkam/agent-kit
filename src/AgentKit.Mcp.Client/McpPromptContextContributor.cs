// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Opens one MCP session per scope and contributes prompt catalog metadata.</summary>
public sealed class McpPromptContextContributor(
    IEnumerable<McpEndpointContextBinding> bindings,
    IMcpClientSessionFactory sessionFactory,
    IMcpEndpointCatalog endpointCatalog,
    IMcpCapabilityProfileCatalog profileCatalog): IContextContributor
{
    private readonly ImmutableArray<McpEndpointContextBinding> _bindings = [.. bindings];

    /// <inheritdoc/>
    public async ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_bindings.IsEmpty)
        {
            return new ContextContribution([], []);
        }

        var candidates = ImmutableArray.CreateBuilder<ContextCandidate>();
        foreach (var binding in _bindings)
        {
            await using var session = await McpContextSessionFactory.OpenReadOnlySessionAsync(
                binding,
                request,
                sessionFactory,
                endpointCatalog,
                profileCatalog,
                cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                continue;
            }

            var catalog = await session.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            var contribution = await new McpPromptSource(catalog).ContributeAsync(request, cancellationToken)
                .ConfigureAwait(false);
            candidates.AddRange(contribution.Candidates);
        }

        return new ContextContribution(candidates.ToImmutable(), []);
    }
}
