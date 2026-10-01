// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Checks a compactor's strategy before/after constraints and a profile's explicit order against them.</summary>
/// <remarks>
/// A constraint that names a strategy absent from the compactor is soft and ignored, so a registration can state its
/// preferred neighbours without requiring them. The numeric <see cref="CompactionStrategyRegistration.Order"/> only
/// breaks ties in the deterministic sequence used to report a cycle; selection order always comes from a profile's or the
/// compactor's explicit key list.
/// </remarks>
internal static class CompactionStrategyOrdering
{
    /// <summary>Rejects a constraint cycle among <paramref name="registrations"/> and any contradiction in <paramref name="profileOrder"/>.</summary>
    /// <param name="profile">The profile being validated, named in failures.</param>
    /// <param name="registrations">Every strategy registered for the profile's compactor.</param>
    /// <param name="profileOrder">The profile's explicit ordered strategy keys.</param>
    /// <exception cref="InvalidOperationException">The constraints form a cycle, or the profile order places a strategy against a constraint.</exception>
    internal static void Validate(
        CompactionProfileKey profile,
        IReadOnlyCollection<CompactionStrategyRegistration> registrations,
        ImmutableArray<CompactionStrategyKey> profileOrder)
    {
        Debug.Assert(registrations is not null, "Ordering validation requires the compactor's registrations.");
        Debug.Assert(!profileOrder.IsDefault, "A profile declaration always carries an initialized order.");

        var known = registrations.Select(static registration => registration.Descriptor.Key).ToHashSet();
        var edges = new List<(CompactionStrategyKey First, CompactionStrategyKey Second)>();
        foreach (var registration in registrations)
        {
            var key = registration.Descriptor.Key;
            edges.AddRange(registration.Before.Where(known.Contains).Select(before => (before, key)));
            edges.AddRange(registration.After.Where(known.Contains).Select(after => (key, after)));
        }

        ThrowOnCycle(profile, registrations, edges);

        var position = new Dictionary<CompactionStrategyKey, int>();
        for (var index = 0; index < profileOrder.Length; index++)
        {
            position[profileOrder[index]] = index;
        }

        foreach (var (first, second) in edges)
        {
            if (position.TryGetValue(first, out var firstIndex)
                && position.TryGetValue(second, out var secondIndex)
                && firstIndex > secondIndex)
            {
                throw new InvalidOperationException(
                    $"Compaction profile '{profile.Value}' orders strategy '{second.Value}' before '{first.Value}', which contradicts a registered before/after constraint.");
            }
        }
    }

    private static void ThrowOnCycle(
        CompactionProfileKey profile,
        IReadOnlyCollection<CompactionStrategyRegistration> registrations,
        List<(CompactionStrategyKey First, CompactionStrategyKey Second)> edges)
    {
        var remaining = registrations
            .OrderBy(static registration => registration.Order)
            .ThenBy(static registration => registration.Descriptor.Key.Value, StringComparer.Ordinal)
            .Select(static registration => registration.Descriptor.Key)
            .ToList();
        while (remaining.Count > 0)
        {
            var ready = remaining.FirstOrDefault(key => !edges.Any(edge => edge.Second == key && remaining.Contains(edge.First)));
            if (ready == default)
            {
                throw new InvalidOperationException(
                    $"Compaction strategy before/after constraints form a cycle among '{string.Join("', '", remaining.Select(static key => key.Value))}' (reported while validating profile '{profile.Value}').");
            }

            _ = remaining.Remove(ready);
        }
    }
}
