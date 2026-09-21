// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Contributes bounded skill inventory text from the shared <see cref="ISkillCatalog"/>.</summary>
public sealed class SkillInventoryContextContributor: IContextContributor
{
    private readonly ISkillCatalogContextSource _catalog;

    /// <summary>Initializes the contributor.</summary>
    /// <param name="catalog">The catalog that renders discovery inventory text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    public SkillInventoryContextContributor(ISkillCatalogContextSource catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var inventory = _catalog.RenderInventory();
        var bytes = Encoding.UTF8.GetByteCount(inventory);
        var candidate = new ContextCandidate(
            new ContextSourceReference(
                new ContextSourceNamespace("agentkit.tools.skill"),
                new ContextSourceKey("inventory"),
                new ContextSourceVersion(_catalog.CatalogVersion)),
            ContextCandidateKind.ReferenceData,
            ContextTrust.Package,
            priority: 10,
            ContextScope.Run,
            new ContextCostEstimate(bytes, Math.Max(1, bytes / 4)),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerRun,
            mandatory: false,
            [new TextPart(inventory, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        return ValueTask.FromResult(new ContextContribution([candidate], []));
    }
}
