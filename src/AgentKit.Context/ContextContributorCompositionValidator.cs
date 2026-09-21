// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Validates additive context-contributor registrations captured on a service collection.</summary>
public static class ContextContributorCompositionValidator
{
    /// <summary>Collects duplicate contributor identity diagnostics for one service collection.</summary>
    /// <param name="services">The composition under validation.</param>
    /// <returns>Deterministic diagnostics for every duplicate contributor id under the same assembler key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static ImmutableArray<CompositionDiagnostic> ValidateRegistrations(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var declarations = services
            .Select(static descriptor => descriptor.ImplementationInstance as ContextContributorDeclaration)
            .Where(static declaration => declaration is not null)
            .Cast<ContextContributorDeclaration>()
            .ToArray();

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        foreach (var group in declarations.GroupBy(static declaration => (declaration.AssemblerKey, declaration.Registration.ContributorId)))
        {
            var entries = group.ToArray();
            if (entries.Length <= 1)
            {
                continue;
            }

            var types = entries.Select(static entry => entry.ContributorType.Name).Distinct(StringComparer.Ordinal).ToArray();
            if (types.Length > 1)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.context.contributor.duplicate-id",
                    $"Context contributor id '{group.Key.ContributorId.Value}' is registered {entries.Length} times under assembler key '{group.Key.AssemblerKey}' with differing implementations ({string.Join(", ", types)})."));
            }
        }

        foreach (var assemblerGroup in declarations.GroupBy(static declaration => declaration.AssemblerKey))
        {
            var orderGroups = assemblerGroup
                .GroupBy(static declaration => declaration.Registration.Order)
                .Where(static group => group.Count() > 1)
                .ToArray();
            if (orderGroups.Length == 0)
            {
                continue;
            }

            foreach (var orderGroup in orderGroups)
            {
                var ids = orderGroup.Select(static entry => entry.Registration.ContributorId.Value).Order(StringComparer.Ordinal);
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.context.contributor.order-collision",
                    $"Assembler key '{assemblerGroup.Key}' assigns order {orderGroup.Key} to multiple contributors ({string.Join(", ", ids)}). Use distinct order values so evaluation order is unambiguous."));
            }
        }

        return diagnostics.ToImmutable();
    }
}
