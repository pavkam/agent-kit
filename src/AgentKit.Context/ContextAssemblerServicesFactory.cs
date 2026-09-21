// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Builds <see cref="ContextAssemblerServices"/> for one keyed assembler registration.</summary>
internal static class ContextAssemblerServicesFactory
{
    /// <summary>Compiles contributors and collaborators for one assembler key inside the current scope.</summary>
    /// <param name="provider">The active service provider for the run scope.</param>
    /// <param name="assemblerKey">The exact assembler key being resolved.</param>
    /// <returns>The compiled collaborator bundle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assemblerKey"/> is blank.</exception>
    internal static ContextAssemblerServices Create(IServiceProvider provider, string assemblerKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblerKey);

        var declarations = provider.GetServices<ContextContributorDeclaration>()
            .Where(declaration => assemblerKey.Equals(declaration.AssemblerKey, StringComparison.Ordinal))
            .OrderBy(declaration => declaration.Registration.Order)
            .ThenBy(declaration => declaration.Registration.ContributorId.Value, StringComparer.Ordinal)
            .ToArray();

        var contributors = new List<RegisteredContextContributor>(declarations.Length);
        foreach (var declaration in declarations)
        {
            var contributor = (IContextContributor) provider.GetRequiredService(declaration.ContributorType);
            contributors.Add(new RegisteredContextContributor(declaration.Registration, contributor));
        }

        var budgets = provider.GetKeyedService<IContextBudgetAllocator>(assemblerKey)
            ?? provider.GetRequiredService<IContextBudgetAllocator>();
        var options = provider.GetRequiredService<IOptions<AgentContextOptions>>().Value;
        return new ContextAssemblerServices(contributors, budgets, options);
    }
}
