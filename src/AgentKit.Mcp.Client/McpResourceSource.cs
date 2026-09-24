// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Text;

/// <summary>Contributes MCP resource listings as untrusted retrieved context.</summary>
public sealed class McpResourceSource(IMcpClientSession session, McpCatalogSnapshot catalog): IContextContributor
{
    private readonly IMcpClientSession _session = session;
    private readonly McpCatalogSnapshot _catalog = catalog;

    /// <inheritdoc/>
    public ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_catalog.Resources.IsEmpty)
        {
            return ValueTask.FromResult(new ContextContribution([], []));
        }

        var builder = ImmutableArray.CreateBuilder<ContextCandidate>(_catalog.Resources.Length);
        foreach (var resource in _catalog.Resources)
        {
            var text = string.IsNullOrWhiteSpace(resource.Description)
                ? resource.Uri
                : $"{resource.Uri}: {resource.Description}";
            builder.Add(new ContextCandidate(
                new ContextSourceReference(
                    new ContextSourceNamespace("agentkit.mcp.resource"),
                    new ContextSourceKey(resource.Uri),
                    new ContextSourceVersion(_catalog.Version.Value.ToString(CultureInfo.InvariantCulture))),
                ContextCandidateKind.ReferenceData,
                ContextTrust.RetrievedData,
                priority: 0,
                ContextScope.Run,
                new ContextCostEstimate(Encoding.UTF8.GetByteCount(text), 1),
                ContextFreshness.Pinned,
                ContextEvaluationFrequency.OncePerRun,
                mandatory: false,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));
        }

        _ = _session;
        return ValueTask.FromResult(new ContextContribution(builder.ToImmutable(), []));
    }
}
