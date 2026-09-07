// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Checks that a built composition can actually run an agent.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft DI's own build validation proves that registered constructors can
/// be satisfied. It cannot know that AgentKit additionally needs a definition
/// catalog with at least one published agent, and a loop resolvable from a run
/// scope. This validator adds those engine-level requirements.
/// </para>
/// <para>
/// Every problem is collected rather than thrown on first failure, so a
/// misconfigured composition reports its complete set of mistakes once.
/// </para>
/// </remarks>
internal static class AgentCompositionValidator
{
    /// <summary>
    /// Validates that <paramref name="provider"/> can run at least one agent.
    /// </summary>
    /// <param name="provider">The freshly built composition to inspect.</param>
    /// <exception cref="AgentCompositionException">
    /// The composition is missing a required engine-wide service, publishes no
    /// runnable agent definition, or cannot resolve a loop for a run.
    /// </exception>
    public static void Validate(IServiceProvider provider)
    {
        Debug.Assert(provider is not null, "A built provider is required for composition validation.");

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();

        var catalog = Resolve<IAgentDefinitionCatalog>(provider, diagnostics, "agentkit.catalog.missing");
        _ = Resolve<TimeProvider>(provider, diagnostics, "agentkit.time.missing");
        _ = Resolve<IIdentifierGenerator<RunId>>(provider, diagnostics, "agentkit.runid.missing");

        if (catalog is not null)
        {
            ValidateCatalog(catalog, diagnostics);
        }

        ValidateRunScope(provider, diagnostics);

        if (diagnostics.Count > 0)
        {
            throw new AgentCompositionException(diagnostics.ToImmutable());
        }
    }

    private static void ValidateCatalog(
        IAgentDefinitionCatalog catalog,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        var snapshot = catalog.CurrentSnapshot;
        if (snapshot is null)
        {
            diagnostics.Add(new CompositionDiagnostic("agentkit.catalog.not-ready", "The agent definition catalog has no materialized bootstrap snapshot."));
            return;
        }

        if (snapshot.Definitions.IsEmpty)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.catalog.empty",
                "No agent definition is published. Register at least one with AddAgent."));
        }
    }

    private static void ValidateRunScope(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        // The loop is resolved per run from a scope, so validating it against
        // the root provider would miss scoped dependencies entirely.
        using var scope = provider.CreateScope();

        try
        {
            _ = scope.ServiceProvider.GetRequiredService<IAgentLoop>();
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.loop.unresolvable",
                $"No {nameof(IAgentLoop)} can be resolved for a run scope. Register one with "
                + $"AddAgentLoop and register its collaborators. {exception.Message}"));
        }
    }

    private static TService? Resolve<TService>(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics,
        string code)
        where TService : class
    {
        var service = provider.GetService<TService>();
        if (service is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"No {typeof(TService).Name} is registered. Call AddAgentKit on the service collection."));
        }

        return service;
    }
}
