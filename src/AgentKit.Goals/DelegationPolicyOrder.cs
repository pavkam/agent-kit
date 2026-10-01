// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Computes the deterministic evaluation order of the delegation policies a profile selects.</summary>
/// <remarks>The order is a stable topological sort: policies appear in the profile's declared sequence except where a declared predecessor must come first. A cycle or an unregistered selection is reported, never guessed around.</remarks>
internal static class DelegationPolicyOrder
{
    /// <summary>Orders the selected policies.</summary>
    /// <param name="selected">The profile's selected policy identities in declared order.</param>
    /// <param name="declarations">Every registered policy declaration.</param>
    /// <returns>The ordered declarations, or a content-safe description of why no order exists.</returns>
    internal static (ImmutableArray<DelegationPolicyDeclaration> Ordered, string? Problem) Compute(
        ImmutableArray<ComponentId> selected,
        IReadOnlyCollection<DelegationPolicyDeclaration> declarations)
    {
        Debug.Assert(!selected.IsDefault, "A profile always publishes its selected policies.");
        Debug.Assert(declarations is not null, "Registered declarations are supplied.");
        var byId = declarations.ToDictionary(static declaration => declaration.Registration.Id);
        var chosen = new List<DelegationPolicyDeclaration>(selected.Length);
        foreach (var id in selected)
        {
            if (!byId.TryGetValue(id, out var declaration))
            {
                return ([], "A selected delegation policy is not registered.");
            }

            chosen.Add(declaration);
        }

        var ordered = new List<DelegationPolicyDeclaration>(chosen.Count);
        var state = new Dictionary<ComponentId, int>();
        foreach (var declaration in chosen)
        {
            if (!Visit(declaration, chosen, state, ordered))
            {
                return ([], "The selected delegation policies declare a cyclic ordering.");
            }
        }

        return ([.. ordered], null);
    }

    private static bool Visit(
        DelegationPolicyDeclaration declaration,
        List<DelegationPolicyDeclaration> chosen,
        Dictionary<ComponentId, int> state,
        List<DelegationPolicyDeclaration> ordered)
    {
        var id = declaration.Registration.Id;
        if (state.TryGetValue(id, out var existing))
        {
            return existing == 2;
        }

        state[id] = 1;
        foreach (var predecessor in declaration.Registration.After)
        {
            var target = chosen.FirstOrDefault(candidate => candidate.Registration.Id == predecessor);
            if (target is not null && !Visit(target, chosen, state, ordered))
            {
                return false;
            }
        }

        state[id] = 2;
        ordered.Add(declaration);
        return true;
    }
}
