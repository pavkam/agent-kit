// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Validates declared component registrations as a pure, closed dependency graph.
/// </summary>
/// <remarks>
/// This validator never constructs a provider, invokes a factory, or performs I/O. It proves the
/// descriptor relationships that Microsoft DI cannot infer from opaque registrations: exact keyed
/// cardinality, cycles, factory-operation re-entry, and singleton capture of scoped state.
/// </remarks>
internal static class ComponentDependencyGraphValidator
{
    /// <summary>Validates a build-local graph after materializing explicitly permitted Microsoft DI infrastructure evidence.</summary>
    /// <param name="snapshot">The non-null frozen component and Microsoft DI registration snapshot.</param>
    /// <returns>Every deterministic materialization and graph diagnostic found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    internal static ImmutableArray<CompositionDiagnostic> ValidateSnapshot(ComponentRegistrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var (Registrations, Diagnostics, DependencyTargets) =
            ComponentInfrastructureGraphMaterializer.Materialize(snapshot);
        return [.. Diagnostics, .. Validate(Registrations, DependencyTargets)];
    }

    /// <summary>Validates a materialized set of component registration descriptors.</summary>
    /// <param name="registrations">The initialized, non-null descriptors to validate.</param>
    /// <returns>Every deterministic diagnostic found; an empty array means the declared graph is valid.</returns>
    /// <exception cref="ArgumentException"><paramref name="registrations"/> is default or contains a null descriptor.</exception>
    public static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        ArgumentException.ThrowIfContainsNull(registrations);
        return Validate(registrations, dependencyTargets: null);
    }

    private static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        ImmutableDictionary<(int OwnerIndex, int DependencyIndex), ImmutableArray<int>>? dependencyTargets)
    {
        Debug.Assert(!registrations.IsDefault,
            "The public or snapshot validator rejects a default registration collection.");
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        var registrationsByService = IndexRegistrations(registrations);
        var edges = CreateEdges(registrations.Length);
        var ordinaryEdges = CreateEdges(registrations.Length);

        for (var index = 0; index < registrations.Length; index++)
        {
            var registration = registrations[index];
            for (var dependencyIndex = 0; dependencyIndex < registration.Dependencies.Length; dependencyIndex++)
            {
                var dependency = registration.Dependencies[dependencyIndex];
                var targets = dependencyTargets is null
                    ? Resolve(registrationsByService, dependency.Reference)
                    : ResolveMaterialized(dependencyTargets, index, dependencyIndex);
                ValidateCardinality(index, dependency, targets, registrations, diagnostics);
                AddEdges(index, dependency, targets, edges, ordinaryEdges);
                ValidateFactoryBoundary(index, dependency, targets, registrationsByService, registrations, diagnostics);
            }
        }

        ValidateCycles(registrations, edges, diagnostics);
        ValidateCaptiveScopes(registrations, ordinaryEdges, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static List<int> ResolveMaterialized(
        ImmutableDictionary<(int OwnerIndex, int DependencyIndex), ImmutableArray<int>> dependencyTargets,
        int ownerIndex,
        int dependencyIndex)
    {
        Debug.Assert(dependencyTargets is not null, "Snapshot materialization supplies initialized edge targets.");
        Debug.Assert(ownerIndex >= 0, "A dependency owner index cannot be negative.");
        Debug.Assert(dependencyIndex >= 0, "A dependency index cannot be negative.");
        var found = dependencyTargets.TryGetValue((ownerIndex, dependencyIndex), out var targets);
        Debug.Assert(found, "Snapshot materialization records target provenance for every dependency edge.");
        return found ? [.. targets] : [];
    }

    /// <summary>Indexes descriptors by their exact closed service contract and key.</summary>
    /// <param name="registrations">The validated non-default registration descriptors.</param>
    /// <returns>Lists retaining the declaration indexes for every exact service address.</returns>
    private static Dictionary<ComponentContractReference, List<int>> IndexRegistrations(
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        Debug.Assert(!registrations.IsDefault, "The public validator entry point rejects a default descriptor array.");
        var registrationsByService = new Dictionary<ComponentContractReference, List<int>>();
        for (var index = 0; index < registrations.Length; index++)
        {
            var service = registrations[index].Service;
            if (!registrationsByService.TryGetValue(service, out var matches))
            {
                matches = [];
                registrationsByService.Add(service, matches);
            }

            matches.Add(index);
        }

        return registrationsByService;
    }

    /// <summary>Creates initialized empty adjacency lists for a graph of the requested size.</summary>
    /// <param name="count">The non-negative number of graph nodes.</param>
    /// <returns>One mutable empty edge list for every node index.</returns>
    private static List<int>[] CreateEdges(int count)
    {
        Debug.Assert(count >= 0, "A registration count cannot be negative.");
        var edges = new List<int>[count];
        for (var index = 0; index < count; index++)
        {
            edges[index] = [];
        }

        return edges;
    }

    /// <summary>Resolves registrations whose service address exactly equals a dependency reference.</summary>
    /// <param name="registrationsByService">The initialized exact-address index.</param>
    /// <param name="reference">The non-null dependency address to resolve.</param>
    /// <returns>The declared target indexes, or an empty initialized list when none match.</returns>
    private static List<int> Resolve(
        IReadOnlyDictionary<ComponentContractReference, List<int>> registrationsByService,
        ComponentContractReference reference) =>
        registrationsByService.TryGetValue(reference, out var matches) ? matches : [];

    /// <summary>Emits a diagnostic when a singular dependency resolves outside its required or optional bounds.</summary>
    /// <param name="ownerIndex">The index of the registration that declares the dependency.</param>
    /// <param name="dependency">The non-null dependency declaration being resolved.</param>
    /// <param name="targets">The initialized exact matches for the dependency.</param>
    /// <param name="registrations">The descriptors used to describe the owning registration.</param>
    /// <param name="diagnostics">The mutable result collector owned by the public validation call.</param>
    private static void ValidateCardinality(
        int ownerIndex,
        ComponentDependencyDescriptor dependency,
        List<int> targets,
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert((uint) ownerIndex < (uint) registrations.Length, "A dependency owner index must identify a declared registration.");
        Debug.Assert(dependency is not null, "Registration descriptors reject null dependency declarations.");
        Debug.Assert(targets is not null, "Dependency resolution always returns an initialized target collection.");
        if (dependency.Cardinality == ComponentDependencyCardinality.AdditiveCollection)
        {
            return;
        }

        if (targets.Count == 0
            && dependency.Cardinality == ComponentDependencyCardinality.RequiredSingular)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-dependency.missing",
                $"{Describe(registrations[ownerIndex])} requires exactly one {Describe(dependency.Reference)}, but none is registered."));
        }
        else if (targets.Count > 1)
        {
            var cardinalityDescription = dependency.Cardinality == ComponentDependencyCardinality.RequiredSingular
                ? "requires exactly one"
                : "permits at most one";
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-dependency.ambiguous",
                $"{Describe(registrations[ownerIndex])} {cardinalityDescription} {Describe(dependency.Reference)}, but {targets.Count} registrations match."));
        }
    }

    /// <summary>Adds graph edges for every declared matching dependency target, including ambiguous singular evidence.</summary>
    /// <param name="ownerIndex">The index of the registration that consumes the dependency.</param>
    /// <param name="dependency">The non-null dependency declaration.</param>
    /// <param name="targets">The initialized matching target indexes.</param>
    /// <param name="edges">The full graph, including factory boundaries for re-entry checks.</param>
    /// <param name="ordinaryEdges">The ordinary construction graph, excluding operation-owned factory boundaries.</param>
    private static void AddEdges(
        int ownerIndex,
        ComponentDependencyDescriptor dependency,
        List<int> targets,
        List<int>[] edges,
        List<int>[] ordinaryEdges)
    {
        Debug.Assert((uint) ownerIndex < (uint) edges.Length, "A dependency owner index must identify an edge list.");
        Debug.Assert(edges.Length == ordinaryEdges.Length, "All graph views must cover the same registrations.");
        Debug.Assert(targets is not null, "Dependency resolution always returns an initialized target collection.");
        foreach (var target in targets)
        {
            edges[ownerIndex].Add(target);
            if (dependency.FactoryBoundary is null)
            {
                ordinaryEdges[ownerIndex].Add(target);
            }
        }
    }

    /// <summary>Checks that explicit factory metadata identifies one owned, disposable operation root.</summary>
    /// <param name="ownerIndex">The index of the descriptor that declares the factory dependency.</param>
    /// <param name="dependency">The non-null dependency whose optional boundary is being checked.</param>
    /// <param name="targets">The initialized exact matches for the dependency.</param>
    /// <param name="registrationsByService">The initialized exact-address index.</param>
    /// <param name="registrations">The descriptors used for boundary evidence.</param>
    /// <param name="diagnostics">The mutable result collector owned by the public validation call.</param>
    private static void ValidateFactoryBoundary(
        int ownerIndex,
        ComponentDependencyDescriptor dependency,
        List<int> targets,
        IReadOnlyDictionary<ComponentContractReference, List<int>> registrationsByService,
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert((uint) ownerIndex < (uint) registrations.Length, "A factory boundary owner index must identify a declared registration.");
        Debug.Assert(targets is not null, "Dependency resolution always returns an initialized target collection.");
        if (dependency.FactoryBoundary is not { } boundary)
        {
            return;
        }

        var owner = registrations[ownerIndex];
        if (!owner.Service.Equals(boundary.Owner))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.owner-mismatch",
                $"The factory boundary declared by {Describe(owner)} names {Describe(boundary.Owner)} as its owner."));
        }

        if (Resolve(registrationsByService, boundary.Owner).Count == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.owner-missing",
                $"The factory boundary owner {Describe(boundary.Owner)} is not registered."));
        }

        if (dependency.Cardinality != ComponentDependencyCardinality.RequiredSingular
            || !dependency.Reference.Equals(boundary.OperationRoot))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.root-mismatch",
                $"The factory boundary on {Describe(owner)} must name its required singular dependency {Describe(dependency.Reference)} as the operation root."));
            return;
        }

        if (targets.Count == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.root-missing",
                $"The factory boundary operation root {Describe(boundary.OperationRoot)} is not registered."));
            return;
        }

        if (targets.Count > 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.root-ambiguous",
                $"The factory boundary operation root {Describe(boundary.OperationRoot)} has {targets.Count} matching registrations."));
            return;
        }

        var operationRoot = registrations[targets[0]];
        if (!boundary.DisposalContractType.IsAssignableFrom(operationRoot.ImplementationType))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-factory-boundary.disposal-contract",
                $"The factory boundary operation root {Describe(operationRoot)} does not implement {boundary.DisposalContractType.Name}."));
        }
    }

    /// <summary>Finds every cyclic strongly connected component and emits its deterministic complete path.</summary>
    /// <param name="registrations">The descriptors named in cycle diagnostics.</param>
    /// <param name="edges">The initialized full dependency graph.</param>
    /// <param name="diagnostics">The mutable result collector owned by the public validation call.</param>
    private static void ValidateCycles(
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        List<int>[] edges,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(edges.Length == registrations.Length, "The graph must have one adjacency list for every registration.");
        foreach (var component in FindStronglyConnectedComponents(edges))
        {
            if (component.Count == 1 && !edges[component[0]].Contains(component[0]))
            {
                continue;
            }

            var path = FindCyclePath(component, edges, registrations);
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.component-dependency.cycle",
                $"Component dependency cycle: {string.Join(" -> ", path.Select(index => Describe(registrations[index])))}."));
        }
    }

    /// <summary>Finds scoped registrations retained through ordinary singleton construction paths.</summary>
    /// <param name="registrations">The descriptors supplying lifetime and diagnostic information.</param>
    /// <param name="ordinaryEdges">The initialized graph that excludes owned operation boundaries.</param>
    /// <param name="diagnostics">The mutable result collector owned by the public validation call.</param>
    private static void ValidateCaptiveScopes(
        ImmutableArray<ComponentRegistrationDescriptor> registrations,
        List<int>[] ordinaryEdges,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(ordinaryEdges.Length == registrations.Length, "The ordinary graph must have one adjacency list for every registration.");
        for (var singleton = 0; singleton < registrations.Length; singleton++)
        {
            if (registrations[singleton].Lifetime != ServiceLifetime.Singleton)
            {
                continue;
            }

            var path = FindPathToScoped(singleton, ordinaryEdges, registrations);
            if (path is not null)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.component-lifetime.captive-scoped",
                    $"Singleton {Describe(registrations[singleton])} captures scoped {Describe(registrations[path[^1]])} through {string.Join(" -> ", path.Select(index => Describe(registrations[index])))}."));
            }
        }
    }

    /// <summary>Computes strongly connected components with two iterative graph walks.</summary>
    /// <param name="edges">The initialized directed graph whose node indexes are dense from zero.</param>
    /// <returns>Every component as registration indexes without relying on call-stack recursion.</returns>
    private static ImmutableArray<List<int>> FindStronglyConnectedComponents(List<int>[] edges)
    {
        Debug.Assert(edges is not null, "A materialized graph must supply adjacency lists.");
        var order = new List<int>(edges.Length);
        var visited = new bool[edges.Length];
        for (var start = 0; start < edges.Length; start++)
        {
            if (visited[start])
            {
                continue;
            }

            visited[start] = true;
            var stack = new List<(int Node, int NextEdge)> { (start, 0) };
            while (stack.Count > 0)
            {
                var (node, nextEdge) = stack[^1];
                if (nextEdge == edges[node].Count)
                {
                    order.Add(node);
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                stack[^1] = (node, nextEdge + 1);
                var target = edges[node][nextEdge];
                if (!visited[target])
                {
                    visited[target] = true;
                    stack.Add((target, 0));
                }
            }
        }

        var reverseEdges = CreateEdges(edges.Length);
        for (var source = 0; source < edges.Length; source++)
        {
            foreach (var target in edges[source])
            {
                reverseEdges[target].Add(source);
            }
        }

        Array.Fill(visited, false);
        var components = ImmutableArray.CreateBuilder<List<int>>();
        for (var position = order.Count - 1; position >= 0; position--)
        {
            var start = order[position];
            if (visited[start])
            {
                continue;
            }

            var component = new List<int>();
            var stack = new Stack<int>();
            stack.Push(start);
            visited[start] = true;
            while (stack.TryPop(out var node))
            {
                component.Add(node);
                foreach (var target in reverseEdges[node])
                {
                    if (!visited[target])
                    {
                        visited[target] = true;
                        stack.Push(target);
                    }
                }
            }

            components.Add(component);
        }

        return components.ToImmutable();
    }

    /// <summary>Finds one complete cycle path within a known cyclic strongly connected component.</summary>
    /// <param name="component">The non-empty cyclic component expressed as graph indexes.</param>
    /// <param name="edges">The initialized full graph containing the component's edges.</param>
    /// <param name="registrations">The descriptors used to choose a deterministic path start.</param>
    /// <returns>A path whose final index equals its first index.</returns>
    /// <exception cref="InvalidOperationException">The supplied component is not actually cyclic despite the caller invariant.</exception>
    private static IReadOnlyList<int> FindCyclePath(
        List<int> component,
        List<int>[] edges,
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        Debug.Assert(component.Count > 0, "Only non-empty strongly connected components can be asked for a cycle path.");
        Debug.Assert(edges.Length == registrations.Length, "Cycle-path graph and registrations must use the same indexes.");
        var members = component.ToHashSet();
        var start = component.OrderBy(index => Describe(registrations[index]), StringComparer.Ordinal).ThenBy(static index => index).First();
        var visited = new HashSet<int> { start };
        var stack = new List<(int Node, int NextEdge)> { (start, 0) };
        while (stack.Count > 0)
        {
            var (node, nextEdge) = stack[^1];
            if (nextEdge == edges[node].Count)
            {
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack[^1] = (node, nextEdge + 1);
            var target = edges[node][nextEdge];
            if (!members.Contains(target))
            {
                continue;
            }

            if (target == start)
            {
                return [.. stack.Select(static item => item.Node), start];
            }

            if (visited.Add(target))
            {
                stack.Add((target, 0));
            }
        }

        Debug.Assert(false, "A cyclic strongly connected component must contain a path back to its chosen start node.");
        throw new InvalidOperationException("A cyclic strongly connected component must contain a cycle path.");
    }

    /// <summary>Searches one singleton's ordinary dependency closure for its first scoped registration.</summary>
    /// <param name="start">The index of the singleton whose retained dependencies are being inspected.</param>
    /// <param name="ordinaryEdges">The initialized ordinary dependency graph.</param>
    /// <param name="registrations">The descriptors that classify target lifetimes.</param>
    /// <returns>The inclusive path to a scoped registration, or <see langword="null"/> when no scoped target is reachable.</returns>
    private static List<int>? FindPathToScoped(
        int start,
        List<int>[] ordinaryEdges,
        ImmutableArray<ComponentRegistrationDescriptor> registrations)
    {
        Debug.Assert((uint) start < (uint) ordinaryEdges.Length, "A lifetime traversal start must identify a graph node.");
        Debug.Assert(ordinaryEdges.Length == registrations.Length, "Lifetime traversal graph and registrations must use the same indexes.");
        var parents = new int?[ordinaryEdges.Length];
        var visited = new bool[ordinaryEdges.Length];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        visited[start] = true;
        while (queue.TryDequeue(out var node))
        {
            foreach (var target in ordinaryEdges[node])
            {
                if (visited[target])
                {
                    continue;
                }

                parents[target] = node;
                if (registrations[target].Lifetime == ServiceLifetime.Scoped)
                {
                    return BuildPath(start, target, parents);
                }

                visited[target] = true;
                queue.Enqueue(target);
            }
        }

        return null;
    }

    /// <summary>Reconstructs an inclusive graph path from predecessor evidence recorded by breadth-first search.</summary>
    /// <param name="start">The known path origin.</param>
    /// <param name="end">The discovered path destination.</param>
    /// <param name="parents">The predecessor indexes established for visited nodes.</param>
    /// <returns>The ordered indexes from <paramref name="start"/> through <paramref name="end"/>.</returns>
    private static List<int> BuildPath(int start, int end, int?[] parents)
    {
        Debug.Assert((uint) start < (uint) parents.Length && (uint) end < (uint) parents.Length, "Path endpoints must identify graph nodes.");
        var path = new List<int> { end };
        for (var current = end; current != start;)
        {
            var parent = parents[current];
            Debug.Assert(parent is not null, "A discovered graph path must retain every predecessor.");
            current = parent.Value;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    /// <summary>Formats a descriptor address and concrete implementation for a safe composition diagnostic.</summary>
    /// <param name="registration">The non-null descriptor to identify.</param>
    /// <returns>A stable, non-secret textual identifier for the descriptor.</returns>
    private static string Describe(ComponentRegistrationDescriptor registration) =>
        $"{Describe(registration.Service)} ({registration.ImplementationType.FullName ?? registration.ImplementationType.Name})";

    /// <summary>Formats an exact contract-and-key reference for a safe composition diagnostic.</summary>
    /// <param name="reference">The non-null closed contract reference to identify.</param>
    /// <returns>A stable text representation that includes the key when present.</returns>
    private static string Describe(ComponentContractReference reference) =>
        reference.Key is null
            ? reference.ContractType.FullName ?? reference.ContractType.Name
            : $"{reference.ContractType.FullName ?? reference.ContractType.Name} key '{reference.Key}'";
}
