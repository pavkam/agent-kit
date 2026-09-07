// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// Resolves a set of hooks registered for one hook point into a single
/// deterministic invocation order.
/// </summary>
/// <remarks>
/// This is a pure, stateless computation over the ordering metadata each
/// hook declares (<see cref="IHook.Priority"/>, <see cref="IHook.RunsBefore"/>,
/// <see cref="IHook.RunsAfter"/>, <see cref="IHook.DependsOn"/>) plus
/// registration order as the deterministic tie-breaker; it holds no state
/// of its own and every method is safe to call concurrently.
/// </remarks>
internal static class HookOrdering
{
    /// <summary>Resolves <paramref name="hooks"/> into a single deterministic invocation order.</summary>
    /// <typeparam name="THook">The hook interface for this point.</typeparam>
    /// <param name="hooks">The hooks registered for one hook point, in registration order.</param>
    /// <returns>The same hooks, reordered to satisfy every declared ordering constraint.</returns>
    /// <exception cref="HookCompositionException">
    /// Two hooks share a <see cref="HookId"/>, more than one hook declares
    /// <see cref="HookPriority.First"/> or <see cref="HookPriority.Last"/>,
    /// a <see cref="IHook.DependsOn"/> target is not present, or the
    /// constraints form a cycle.
    /// </exception>
    public static ImmutableArray<THook> Sort<THook>(IReadOnlyList<THook> hooks)
        where THook : IHook
    {
        var indexOf = new Dictionary<HookId, int>(hooks.Count);
        for (var i = 0; i < hooks.Count; i++)
        {
            if (!indexOf.TryAdd(hooks[i].Id, i))
            {
                throw new HookCompositionException($"Duplicate hook id '{hooks[i].Id}' registered for the same hook point.");
            }
        }

        var firstIds = hooks.Where(static h => h.Priority == HookPriority.First).Select(static h => h.Id).ToImmutableArray();
        if (firstIds.Length > 1)
        {
            throw new HookCompositionException("More than one hook declared HookPriority.First for the same hook point.");
        }

        var lastIds = hooks.Where(static h => h.Priority == HookPriority.Last).Select(static h => h.Id).ToImmutableArray();
        if (lastIds.Length > 1)
        {
            throw new HookCompositionException("More than one hook declared HookPriority.Last for the same hook point.");
        }

        var edges = hooks.ToDictionary(static h => h.Id, static _ => new HashSet<HookId>());
        var inDegree = hooks.ToDictionary(static h => h.Id, static _ => 0);

        void AddEdge(HookId before, HookId after)
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

        foreach (var hook in hooks)
        {
            foreach (var beforeTarget in hook.RunsBefore)
            {
                if (indexOf.ContainsKey(beforeTarget))
                {
                    AddEdge(hook.Id, beforeTarget);
                }
            }

            foreach (var afterTarget in hook.RunsAfter)
            {
                if (indexOf.ContainsKey(afterTarget))
                {
                    AddEdge(afterTarget, hook.Id);
                }
            }

            foreach (var dependency in hook.DependsOn)
            {
                if (!indexOf.ContainsKey(dependency))
                {
                    throw new HookCompositionException(
                        $"Hook '{hook.Id}' depends on '{dependency}', which is not registered for this hook point.");
                }

                AddEdge(dependency, hook.Id);
            }
        }

        if (firstIds.Length == 1)
        {
            foreach (var hook in hooks)
            {
                AddEdge(firstIds[0], hook.Id);
            }
        }

        if (lastIds.Length == 1)
        {
            foreach (var hook in hooks)
            {
                AddEdge(hook.Id, lastIds[0]);
            }
        }

        return TopologicalSort(hooks, indexOf, edges, inDegree);
    }

    private static ImmutableArray<THook> TopologicalSort<THook>(
        IReadOnlyList<THook> hooks,
        Dictionary<HookId, int> indexOf,
        Dictionary<HookId, HashSet<HookId>> edges,
        Dictionary<HookId, int> inDegree)
        where THook : IHook
    {
        var remainingInDegree = new Dictionary<HookId, int>(inDegree);
        var ready = new SortedSet<int>(hooks.Where(h => remainingInDegree[h.Id] == 0).Select(h => indexOf[h.Id]));
        var result = ImmutableArray.CreateBuilder<THook>(hooks.Count);

        while (ready.Count > 0)
        {
            var index = ready.Min;
            _ = ready.Remove(index);
            var hook = hooks[index];
            result.Add(hook);

            foreach (var next in edges[hook.Id])
            {
                if (--remainingInDegree[next] == 0)
                {
                    _ = ready.Add(indexOf[next]);
                }
            }
        }

        return result.Count != hooks.Count
            ? throw new HookCompositionException("Hook ordering constraints for this hook point form a cycle.")
            : result.ToImmutable();
    }
}
