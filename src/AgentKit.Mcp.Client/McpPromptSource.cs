// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Text;

/// <summary>Contributes MCP prompt listings as classified user-selectable context.</summary>
public sealed class McpPromptSource(McpCatalogSnapshot catalog): IContextContributor
{
    private readonly McpCatalogSnapshot _catalog = catalog;

    /// <inheritdoc/>
    public ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_catalog.Prompts.IsEmpty)
        {
            return ValueTask.FromResult(new ContextContribution([], []));
        }

        var builder = ImmutableArray.CreateBuilder<ContextCandidate>(_catalog.Prompts.Length);
        foreach (var prompt in _catalog.Prompts)
        {
            var text = string.IsNullOrWhiteSpace(prompt.Description) ? prompt.Name : $"{prompt.Name}: {prompt.Description}";
            builder.Add(new ContextCandidate(
                new ContextSourceReference(
                    new ContextSourceNamespace("agentkit.mcp.prompt"),
                    new ContextSourceKey(prompt.Name),
                    new ContextSourceVersion(_catalog.Version.Value.ToString(CultureInfo.InvariantCulture))),
                ContextCandidateKind.Instruction,
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

        return ValueTask.FromResult(new ContextContribution(builder.ToImmutable(), []));
    }
}
