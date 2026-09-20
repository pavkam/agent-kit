// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// Computes one deterministic dispatch order for hook registrations that target the same point.
/// </summary>
/// <remarks>
/// <para>
/// This type holds no mutable state. Every call depends only on the supplied descriptors, so one instance is safe
/// to register as a singleton and to call concurrently. A default <see cref="ImmutableArray{T}"/> of ordering
/// edges is treated as no edges. Soft <c>Before</c> and <c>After</c> targets that are absent from the resolution
/// are ignored. Hard <c>DependsOn</c> targets that are absent, duplicate identities, more than one
/// <see cref="HookOrderAnchor.First"/> or <see cref="HookOrderAnchor.Last"/>, self-references, contradictory
/// relations, edges that fight an anchor, and cycles all become
/// <see cref="HookOrderInvalid"/> diagnostics with the codes in <see cref="HookOrderDiagnosticCodes"/>. The
/// resolver reports every detected cause together rather than stopping at the first.
/// </para>
/// <para>
/// One descriptor cannot claim both anchors: <see cref="HookOrder"/> carries a single
/// <see cref="HookOrder.Anchor"/>. Discovery order is the tie-breaker after every edge. Conflicting edges are
/// diagnosed and are not dropped in order to force a successful sort.
/// </para>
/// </remarks>
public sealed class HookOrderResolver: IHookOrderResolver
{
    /// <summary>Initializes a new instance of the <see cref="HookOrderResolver"/> class.</summary>
    public HookOrderResolver()
    {
    }

    /// <summary>Resolves one deterministic order for the supplied registrations.</summary>
    /// <param name="registrations">The registrations targeting one hook point, in discovery order.</param>
    /// <returns>The resolved order, or the diagnostics describing why no order satisfies every constraint.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="registrations"/> is default or contains a null element.
    /// </exception>
    public HookOrderResult Resolve(ImmutableArray<HookRegistrationDescriptor> registrations)
    {
        ArgumentException.ThrowIfContainsNull(registrations);

        if (registrations.IsEmpty)
        {
            return new HookOrderResolved([]);
        }

        var diagnostics = new List<CompositionDiagnostic>();
        var indexOf = new Dictionary<HookRegistrationId, int>(registrations.Length);
        var unique = new List<HookRegistrationDescriptor>(registrations.Length);
        foreach (var registration in registrations)
        {
            if (!indexOf.TryAdd(registration.Id, unique.Count))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    HookOrderDiagnosticCodes.DuplicateRegistration,
                    $"Hook registration '{registration.Id}' is duplicated in the same point resolution."));
                continue;
            }

            unique.Add(registration);
        }

        foreach (var registration in unique)
        {
            DiagnoseEdges(registration, indexOf, diagnostics);
        }

        var firsts = new List<HookRegistrationDescriptor>();
        var lasts = new List<HookRegistrationDescriptor>();
        foreach (var registration in unique)
        {
            switch (registration.Order.Anchor)
            {
                case HookOrderAnchor.First:
                    firsts.Add(registration);
                    break;
                case HookOrderAnchor.Last:
                    lasts.Add(registration);
                    break;
                case HookOrderAnchor.Normal:
                    break;
                default:
                    throw new InvalidOperationException($"Hook registration '{registration.Id}' has an undefined order anchor.");
            }
        }

        if (firsts.Count > 1)
        {
            diagnostics.Add(AnchorClaim(HookOrderDiagnosticCodes.MultipleFirst, "First", firsts));
        }

        if (lasts.Count > 1)
        {
            diagnostics.Add(AnchorClaim(HookOrderDiagnosticCodes.MultipleLast, "Last", lasts));
        }

        DiagnoseAnchorContradictions(unique, indexOf, firsts, lasts, diagnostics);

        var ordered = Sort(unique, indexOf, firsts, lasts, diagnostics);
        return diagnostics.Count == 0
            ? new HookOrderResolved(ordered)
            : new HookOrderInvalid([.. diagnostics]);
    }

    private static void DiagnoseEdges(
        HookRegistrationDescriptor registration,
        Dictionary<HookRegistrationId, int> indexOf,
        List<CompositionDiagnostic> diagnostics)
    {
        var reportedContradictions = new HashSet<HookRegistrationId>();
        DiagnoseRelation(registration, registration.Before, "Before", reportedContradictions, diagnostics);
        DiagnoseRelation(registration, registration.After, "After", reportedContradictions, diagnostics);
        DiagnoseRelation(registration, registration.DependsOn, "DependsOn", reportedContradictions, diagnostics);

        foreach (var target in Edges(registration.DependsOn))
        {
            if (target.Equals(registration.Id) || indexOf.ContainsKey(target))
            {
                continue;
            }

            diagnostics.Add(new CompositionDiagnostic(
                HookOrderDiagnosticCodes.MissingDependency,
                $"Hook registration '{registration.Id}' depends on '{target}', which is not in this point resolution."));
        }
    }

    private static void DiagnoseRelation(
        HookRegistrationDescriptor registration,
        ImmutableArray<HookRegistrationId> relation,
        string relationName,
        HashSet<HookRegistrationId> reportedContradictions,
        List<CompositionDiagnostic> diagnostics)
    {
        foreach (var target in Edges(relation))
        {
            if (target.Equals(registration.Id))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    HookOrderDiagnosticCodes.SelfReference,
                    $"Hook registration '{registration.Id}' names itself in {relationName}."));
                continue;
            }

            if (!IsContradictory(registration, target) || !reportedContradictions.Add(target))
            {
                continue;
            }

            if (Contains(registration.Before, target) && Contains(registration.After, target))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    HookOrderDiagnosticCodes.ContradictoryEdges,
                    $"Hook registration '{registration.Id}' names '{target}' in both Before and After."));
            }

            if (Contains(registration.Before, target) && Contains(registration.DependsOn, target))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    HookOrderDiagnosticCodes.ContradictoryEdges,
                    $"Hook registration '{registration.Id}' names '{target}' in both Before and DependsOn."));
            }
        }
    }

    private static void DiagnoseAnchorContradictions(
        List<HookRegistrationDescriptor> unique,
        Dictionary<HookRegistrationId, int> indexOf,
        List<HookRegistrationDescriptor> firsts,
        List<HookRegistrationDescriptor> lasts,
        List<CompositionDiagnostic> diagnostics)
    {
        HookRegistrationId? singleFirst = firsts.Count == 1 ? firsts[0].Id : null;
        HookRegistrationId? singleLast = lasts.Count == 1 ? lasts[0].Id : null;

        foreach (var registration in unique)
        {
            if (registration.Order.Anchor == HookOrderAnchor.First)
            {
                foreach (var target in Edges(registration.After))
                {
                    if (target.Equals(registration.Id) || !indexOf.ContainsKey(target))
                    {
                        continue;
                    }

                    diagnostics.Add(AnchorContradiction(
                        $"First-anchored hook registration '{registration.Id}' cannot be ordered after '{target}'."));
                }

                foreach (var target in Edges(registration.DependsOn))
                {
                    if (target.Equals(registration.Id))
                    {
                        continue;
                    }

                    diagnostics.Add(AnchorContradiction(
                        $"First-anchored hook registration '{registration.Id}' depends on '{target}', which would have to run first."));
                }
            }

            if (registration.Order.Anchor == HookOrderAnchor.Last)
            {
                foreach (var target in Edges(registration.Before))
                {
                    if (target.Equals(registration.Id) || !indexOf.ContainsKey(target))
                    {
                        continue;
                    }

                    diagnostics.Add(AnchorContradiction(
                        $"Last-anchored hook registration '{registration.Id}' cannot be ordered before '{target}'."));
                }
            }

            if (singleFirst is { } first && !registration.Id.Equals(first))
            {
                foreach (var target in Edges(registration.Before))
                {
                    if (target.Equals(first))
                    {
                        diagnostics.Add(AnchorContradiction(
                            $"Hook registration '{registration.Id}' cannot run before the First anchor '{first}'."));
                    }
                }
            }

            if (singleLast is { } last && !registration.Id.Equals(last))
            {
                foreach (var target in Edges(registration.After))
                {
                    if (target.Equals(last))
                    {
                        diagnostics.Add(AnchorContradiction(
                            $"Hook registration '{registration.Id}' cannot run after the Last anchor '{last}'."));
                    }
                }

                foreach (var target in Edges(registration.DependsOn))
                {
                    if (target.Equals(last))
                    {
                        diagnostics.Add(AnchorContradiction(
                            $"Hook registration '{registration.Id}' depends on the Last anchor '{last}', which would have to run first."));
                    }
                }
            }
        }
    }

    private static ImmutableArray<HookRegistrationDescriptor> Sort(
        List<HookRegistrationDescriptor> unique,
        Dictionary<HookRegistrationId, int> indexOf,
        List<HookRegistrationDescriptor> firsts,
        List<HookRegistrationDescriptor> lasts,
        List<CompositionDiagnostic> diagnostics)
    {
        Debug.Assert(unique.Count > 0, "Sort is only called for a non-empty unique registration set.");

        var edges = new Dictionary<HookRegistrationId, HashSet<HookRegistrationId>>(unique.Count);
        var inDegree = new Dictionary<HookRegistrationId, int>(unique.Count);
        foreach (var registration in unique)
        {
            edges[registration.Id] = [];
            inDegree[registration.Id] = 0;
        }

        void AddEdge(HookRegistrationId before, HookRegistrationId after)
        {
            if (before.Equals(after))
            {
                return;
            }

            if (edges[before].Add(after))
            {
                inDegree[after]++;
            }
        }

        foreach (var registration in unique)
        {
            foreach (var target in Edges(registration.Before))
            {
                if (SkipEdge(registration, target, indexOf))
                {
                    continue;
                }

                AddEdge(registration.Id, target);
            }

            foreach (var target in Edges(registration.After))
            {
                if (SkipEdge(registration, target, indexOf))
                {
                    continue;
                }

                AddEdge(target, registration.Id);
            }

            foreach (var target in Edges(registration.DependsOn))
            {
                if (SkipEdge(registration, target, indexOf))
                {
                    continue;
                }

                AddEdge(target, registration.Id);
            }
        }

        if (firsts.Count == 1)
        {
            var first = firsts[0].Id;
            foreach (var registration in unique)
            {
                AddEdge(first, registration.Id);
            }
        }

        if (lasts.Count == 1)
        {
            var last = lasts[0].Id;
            foreach (var registration in unique)
            {
                AddEdge(registration.Id, last);
            }
        }

        var remaining = new Dictionary<HookRegistrationId, int>(inDegree);
        var ready = new SortedSet<int>();
        foreach (var registration in unique)
        {
            if (remaining[registration.Id] == 0)
            {
                _ = ready.Add(indexOf[registration.Id]);
            }
        }

        var ordered = new List<HookRegistrationDescriptor>(unique.Count);
        var placed = new HashSet<HookRegistrationId>(unique.Count);
        while (ready.Count > 0)
        {
            var index = ready.Min;
            _ = ready.Remove(index);
            var registration = unique[index];
            ordered.Add(registration);
            _ = placed.Add(registration.Id);

            foreach (var next in edges[registration.Id])
            {
                if (--remaining[next] == 0)
                {
                    _ = ready.Add(indexOf[next]);
                }
            }
        }

        if (ordered.Count == unique.Count)
        {
            return [.. ordered];
        }

        var involved = new List<string>();
        foreach (var registration in unique)
        {
            if (placed.Contains(registration.Id))
            {
                continue;
            }

            involved.Add($"'{registration.Id}'");
        }

        diagnostics.Add(new CompositionDiagnostic(
            HookOrderDiagnosticCodes.Cycle,
            $"Hook ordering constraints form a cycle involving {string.Join(", ", involved)}."));
        return [];
    }

    private static bool SkipEdge(
        HookRegistrationDescriptor registration,
        HookRegistrationId target,
        Dictionary<HookRegistrationId, int> indexOf) =>
        target.Equals(registration.Id) || !indexOf.ContainsKey(target) || IsContradictory(registration, target);

    private static bool IsContradictory(HookRegistrationDescriptor registration, HookRegistrationId target)
    {
        var inBefore = Contains(registration.Before, target);
        return inBefore && (Contains(registration.After, target) || Contains(registration.DependsOn, target));
    }

    private static bool Contains(ImmutableArray<HookRegistrationId> edges, HookRegistrationId id)
    {
        foreach (var edge in Edges(edges))
        {
            if (edge.Equals(id))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<HookRegistrationId> Edges(ImmutableArray<HookRegistrationId> edges) =>
        edges.IsDefault ? [] : edges;

    private static CompositionDiagnostic AnchorClaim(
        string code,
        string anchorName,
        List<HookRegistrationDescriptor> claimants)
    {
        var ids = new string[claimants.Count];
        for (var i = 0; i < claimants.Count; i++)
        {
            ids[i] = $"'{claimants[i].Id}'";
        }

        return new CompositionDiagnostic(
            code,
            $"More than one hook registration claims the {anchorName} anchor: {string.Join(", ", ids)}.");
    }

    private static CompositionDiagnostic AnchorContradiction(string safeMessage) =>
        new(HookOrderDiagnosticCodes.AnchorContradiction, safeMessage);
}
